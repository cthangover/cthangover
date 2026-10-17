using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Loads and persists <see cref="ModCollection"/> definitions from the three
    /// distribution channels:
    /// <list type="bullet">
    /// <item>mod folders — any mod may ship <c>collections/*.json</c>, read
    /// through <see cref="IModFileProvider"/> so zip mods work too;</item>
    /// <item>online catalogs — cached by <see cref="CatalogService"/>;</item>
    /// <item>the player — <c>user://collections.json</c>, the seed of the
    /// future in-game constructor.</item>
    /// </list>
    /// <see cref="LoadAll"/> merges the channels by id with explicit precedence:
    /// user &gt; built-in &gt; catalog. That lets a shipping mod override an
    /// outdated online collection without asking the catalog owner, and lets the
    /// player edit any collection locally.
    /// </summary>
    public static class ModCollectionStore
    {
        private const string UserFilePath = "user://collections.json";

        /// <summary>
        /// Conventional mod-relative directory scanned for bundled collections.
        /// The directory is reserved: the entity scanner skips it entirely
        /// because collection documents also carry a <c>kind</c> field that is
        /// not an entity kind.
        /// </summary>
        public const string ProviderDirectory = "collections";

        private static string _resolvedPath;

        /// <summary>
        /// Resolves the user storage path once on the main thread before the
        /// background mod-load phase, mirroring <see cref="DisabledModsStore"/>
        /// — <c>ProjectSettings.GlobalizePath</c> is Godot API and is not
        /// available from worker threads.
        /// </summary>
        public static void WarmUp()
        {
            _resolvedPath = GameEnvironment.IsRuntime
                ? ProjectSettings.GlobalizePath(UserFilePath)
                : "collections.json";
        }

        private static string ResolvedPath =>
            _resolvedPath ?? (GameEnvironment.IsRuntime
                ? ProjectSettings.GlobalizePath(UserFilePath)
                : "collections.json");

        /// <summary>Loads player-created collections; returns an empty list when the file is absent or broken.</summary>
        public static List<ModCollection> LoadUser()
        {
            var path = ResolvedPath;
            var result = new List<ModCollection>();
            if (!File.Exists(path))
                return result;

            try
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<ModCollection>>(json, JsonOptionsProvider.ReadOptions);
                if (list == null)
                    return result;

                foreach (var collection in list)
                {
                    if (collection == null || string.IsNullOrEmpty(collection.Id))
                    {
                        GameLogger.Log("MOD_COLLECTIONS", "Skipping user collection without an id", LogLevel.Warning);
                        continue;
                    }

                    collection.Origin = ModCollectionOrigin.User;
                    result.Add(collection);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Failed to load user collections: {ex.Message}", LogLevel.Warning);
            }

            return result;
        }

        /// <summary>Persists the player's collections, replacing the whole file.</summary>
        public static void SaveUser(List<ModCollection> collections)
        {
            try
            {
                var path = ResolvedPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(
                    collections ?? new List<ModCollection>(),
                    JsonOptionsProvider.WriteIndentedIgnoreNullOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Failed to save user collections: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Reads every <c>collections/*.json</c> bundled inside a mod. Used for
        /// both folder and zip providers; a broken file is logged and skipped so
        /// one bad collection cannot hide the others.
        /// </summary>
        public static List<ModCollection> LoadFromProvider(IModFileProvider provider)
        {
            var result = new List<ModCollection>();
            if (provider == null)
                return result;

            foreach (var entry in provider.ListFiles(ProviderDirectory))
            {
                if (entry.EndsWith("/", StringComparison.Ordinal)
                    || !entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var collection = Parse(provider.ReadFileText(entry), $"{provider.Mod}/{entry}");
                if (collection == null)
                    continue;

                collection.Origin = ModCollectionOrigin.BuiltIn;
                collection.SourceModId = provider.Mod;
                result.Add(collection);
            }

            return result;
        }

        /// <summary>
        /// Loads and merges collections from every channel, ordered by precedence
        /// (catalog, then built-in, then user — each step may replace an entry
        /// with the same id). Entries without an id are ignored.
        /// </summary>
        public static List<ModCollection> LoadAll()
        {
            ModRegistry.Instance.Initialize();
            return LoadAll(ModRegistry.Instance.AllDiscoveredMods.Values);
        }

        /// <summary>
        /// Same merge as <see cref="LoadAll()"/> over an explicit mod list. The
        /// boot pipeline uses it while the registry is still being built (the
        /// active game decides which discovered mods survive), so it must not
        /// touch <see cref="ModRegistry"/>.
        /// </summary>
        public static List<ModCollection> LoadAll(IEnumerable<IModInfo> discoveredMods)
        {
            var byId = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);

            var catalog = CatalogService.CachedCollections;
            if (catalog != null)
            {
                foreach (var collection in catalog)
                    AddIfValid(byId, collection, ModCollectionOrigin.Catalog, overwrite: false);
            }

            foreach (var info in discoveredMods ?? Array.Empty<IModInfo>())
            {
                foreach (var collection in LoadFromProvider(info?.FileProvider))
                    AddIfValid(byId, collection, ModCollectionOrigin.BuiltIn, overwrite: true);
            }

            foreach (var collection in LoadUser())
                AddIfValid(byId, collection, ModCollectionOrigin.User, overwrite: true);

            return new List<ModCollection>(byId.Values);
        }

        /// <summary>
        /// True when a mod or catalog ships a collection with this id — the
        /// player's version then only overrides it, and "revert" restores the
        /// shipped one.
        /// </summary>
        public static bool HasShippedVersion(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            if (CatalogService.CachedCollections?.Any(c => string.Equals(c?.Id, id, StringComparison.OrdinalIgnoreCase)) == true)
                return true;

            ModRegistry.Instance.Initialize();
            return ModRegistry.Instance.AllDiscoveredMods.Values
                .Any(info => LoadFromProvider(info?.FileProvider).Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>Drops the player's version of a collection; returns true when one existed.</summary>
        public static bool RemoveUser(string id)
        {
            var user = LoadUser();
            var removed = user.RemoveAll(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                SaveUser(user);
            return removed;
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
                GameLogger.Log("MOD_COLLECTIONS", $"Collection file '{label}' is empty", LogLevel.Warning);
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
