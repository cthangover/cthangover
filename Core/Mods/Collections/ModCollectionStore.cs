using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Loads and persists <see cref="ModCollection"/> definitions. A collection
    /// is its own distribution unit — a <b>package</b> next to the mods, not a
    /// file inside one: a folder or zip named after the collection id that holds
    /// <c>collection.json</c>, the cover image and an optional <c>locale/</c>
    /// with the collection's texts. A game is an assembly of mods by many
    /// authors, so no mod owns it; anyone can ship a game without touching the
    /// mods it uses.
    /// <list type="bullet">
    /// <item>built-in packages — <c>collections/</c> of the game directory
    /// (<c>res://collections/</c>);</item>
    /// <item>the player's packages — <c>user://collections/</c>: edited and
    /// duplicated collections, downloaded ones;</item>
    /// <item>online catalogs — documents cached by <see cref="CatalogService"/>
    /// (no package: no cover or locale files).</item>
    /// </list>
    /// <see cref="LoadAll"/> merges the channels by id with explicit precedence:
    /// user &gt; built-in &gt; catalog, so a shipped game overrides an outdated
    /// online one and the player can edit any collection locally. Packages are
    /// read through <see cref="IModFileProvider"/>, so folders and zips behave
    /// the same. Mods do not ship collections.
    /// </summary>
    public static class ModCollectionStore
    {
        /// <summary>Name of the document inside a package.</summary>
        public const string DocumentName = "collection.json";

        private const string BuiltInRoot = "res://collections/";
        private const string UserRoot = "user://collections/";

        private static string _builtInPath;
        private static string _userPath;

        /// <summary>
        /// Resolves the package roots once on the main thread before the
        /// background mod-load phase, mirroring <see cref="DisabledModsStore"/>
        /// — <c>ProjectSettings.GlobalizePath</c> is Godot API and is not
        /// available from worker threads.
        /// </summary>
        public static void WarmUp()
        {
            _builtInPath = Resolve(BuiltInRoot, "collections");
            _userPath = Resolve(UserRoot, "user_collections");
        }

        private static string Resolve(string godotPath, string fallback) =>
            GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(godotPath) : fallback;

        /// <summary>Absolute folder of the built-in packages.</summary>
        public static string BuiltInPath => _builtInPath ?? Resolve(BuiltInRoot, "collections");

        /// <summary>Absolute folder of the player's packages.</summary>
        public static string UserPath => _userPath ?? Resolve(UserRoot, "user_collections");

        /// <summary>The player's collections (edited, duplicated, downloaded); empty when the folder is absent.</summary>
        public static List<ModCollection> LoadUser() => LoadPackages(UserPath, ModCollectionOrigin.User);

        /// <summary>Collections shipped with the game.</summary>
        public static List<ModCollection> LoadBuiltIn() => LoadPackages(BuiltInPath, ModCollectionOrigin.BuiltIn);

        /// <summary>
        /// Replaces the player's collections with <paramref name="collections"/>:
        /// each becomes the package <c>user://collections/&lt;id&gt;/</c>, and
        /// player packages not in the list are removed. A collection that came
        /// from another package (a fork of a built-in game) takes that package's
        /// cover and <c>locale/</c> files along, so the copy shows the same.
        /// </summary>
        public static void SaveUser(List<ModCollection> collections)
        {
            try
            {
                Directory.CreateDirectory(UserPath);
                var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var collection in collections ?? new List<ModCollection>())
                {
                    if (collection == null || string.IsNullOrEmpty(collection.Id))
                        continue;
                    keep.Add(collection.Id);
                    WritePackage(collection);
                }

                foreach (var dir in Directory.GetDirectories(UserPath))
                {
                    if (!keep.Contains(Path.GetFileName(dir)))
                        Directory.Delete(dir, true);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Failed to save user collections: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Reads every package under <paramref name="root"/>: subfolders and
        /// <c>.zip</c> files holding a <see cref="DocumentName"/>. A broken
        /// package is logged and skipped so one bad collection cannot hide the
        /// others.
        /// </summary>
        public static List<ModCollection> LoadPackages(string root, ModCollectionOrigin origin)
        {
            var result = new List<ModCollection>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return result;

            foreach (var entry in Directory.GetFileSystemEntries(root).OrderBy(e => e, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(entry);
                IModFileProvider package;
                if (Directory.Exists(entry))
                    package = new FolderModFileProvider(entry, name);
                else if (entry.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    package = new ZipModFileProvider(entry, Path.GetFileNameWithoutExtension(name));
                else
                    continue;

                var collection = Parse(package.ReadFileText(DocumentName), $"{name}/{DocumentName}");
                if (collection == null)
                    continue;

                if (!string.Equals(collection.Id, package.Mod, StringComparison.OrdinalIgnoreCase))
                    GameLogger.Log("MOD_COLLECTIONS", $"Package '{name}' holds collection '{collection.Id}'; name the package after the id", LogLevel.Warning);

                collection.Origin = origin;
                collection.Package = package;
                result.Add(collection);
            }

            return result;
        }

        /// <summary>
        /// Loads and merges collections from every channel, ordered by precedence
        /// (catalog, then built-in, then user — each step may replace an entry
        /// with the same id). Entries without an id are ignored. Reads no mod,
        /// so the boot pipeline can call it before the mod set is decided.
        /// </summary>
        public static List<ModCollection> LoadAll()
        {
            var byId = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);

            var catalog = CatalogService.CachedCollections;
            if (catalog != null)
            {
                foreach (var collection in catalog)
                    AddIfValid(byId, collection, ModCollectionOrigin.Catalog, overwrite: false);
            }

            foreach (var collection in LoadBuiltIn())
                AddIfValid(byId, collection, ModCollectionOrigin.BuiltIn, overwrite: true);

            foreach (var collection in LoadUser())
                AddIfValid(byId, collection, ModCollectionOrigin.User, overwrite: true);

            return new List<ModCollection>(byId.Values);
        }

        /// <summary>
        /// True when the game or a catalog ships a collection with this id — the
        /// player's version then only overrides it, and "revert" restores the
        /// shipped one.
        /// </summary>
        public static bool HasShippedVersion(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            if (CatalogService.CachedCollections?.Any(c => string.Equals(c?.Id, id, StringComparison.OrdinalIgnoreCase)) == true)
                return true;

            return LoadBuiltIn().Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Drops the player's package of a collection; returns true when one existed.</summary>
        public static bool RemoveUser(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            var removed = false;
            var dir = Path.Combine(UserPath, id);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                removed = true;
            }

            var zip = dir + ".zip";
            if (File.Exists(zip))
            {
                File.Delete(zip);
                removed = true;
            }

            return removed;
        }

        private static void WritePackage(ModCollection collection)
        {
            var dir = Path.Combine(UserPath, collection.Id);
            Directory.CreateDirectory(dir);

            var source = collection.Package;
            var ownPackage = source is FolderModFileProvider folder
                && string.Equals(Path.GetFullPath(folder.RootPath), Path.GetFullPath(dir), StringComparison.Ordinal);
            if (source != null && !ownPackage)
                CopyPackageFiles(source, dir, collection.Cover);

            var json = JsonSerializer.Serialize(collection, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);
            File.WriteAllText(Path.Combine(dir, DocumentName), json);
            collection.Package = new FolderModFileProvider(dir, collection.Id);
        }

        private static void CopyPackageFiles(IModFileProvider source, string dir, string cover)
        {
            if (!string.IsNullOrEmpty(cover))
                CopyFile(source, cover, dir);

            foreach (var entry in source.ListFiles("locale"))
            {
                if (!entry.EndsWith("/", StringComparison.Ordinal))
                    CopyFile(source, entry, dir);
            }
        }

        private static void CopyFile(IModFileProvider source, string path, string dir)
        {
            var bytes = source.ReadFileBinary(path);
            if (bytes == null)
                return;
            var target = Path.Combine(dir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, bytes);
        }

        private static void AddIfValid(
            Dictionary<string, ModCollection> byId,
            ModCollection collection,
            ModCollectionOrigin origin,
            bool overwrite)
        {
            if (collection == null || string.IsNullOrEmpty(collection.Id))
            {
                GameLogger.Log("MOD_COLLECTIONS", "Skipping collection without an id", LogLevel.Warning);
                return;
            }

            collection.Origin = origin;
            if (overwrite || !byId.ContainsKey(collection.Id))
                byId[collection.Id] = collection;
        }

        private static ModCollection Parse(string json, string label)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Collection file '{label}' is missing or empty", LogLevel.Warning);
                return null;
            }

            try
            {
                var collection = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);
                if (collection == null || string.IsNullOrEmpty(collection.Id))
                {
                    GameLogger.Log("MOD_COLLECTIONS", $"Collection '{label}' has no id", LogLevel.Warning);
                    return null;
                }

                return collection;
            }
            catch (JsonException ex)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Failed to parse collection '{label}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }
    }
}
