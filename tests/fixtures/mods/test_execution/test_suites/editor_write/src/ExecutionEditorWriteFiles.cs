namespace Cthangover.Mods.ExecutionEditorWrite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Tools.Services;

    /// <summary>Owns exact byte backups for the editor's one object file, manifest and locale scope in its physical write lane.</summary>
    public sealed class ExecutionEditorWriteFiles : IDisposable
    {
        /// <summary>Uses existing authored game content; this is an actual editor path, not a substitute document implementation.</summary>
        public const string Mod = "rpg_plus_game";

        /// <summary>The ordinary editor stores newly created RPG objects in this mod-relative file.</summary>
        public const string Objects = "objects/rpg_plus_game.json";

        private readonly SortedDictionary<string, byte[]> originals = new(StringComparer.Ordinal);
        private readonly string before;
        private bool restored;

        /// <summary>Physical folder of the loaded writable mod; all ResourcePaths are relative to this root.</summary>
        public string ResourceRoot { get; }

        /// <summary>Expanded original scoped paths, including the possibly absent destination for the new record.</summary>
        public string[] ResourcePaths => new List<string>(originals.Keys).ToArray();

        /// <summary>Captures physical bytes before the ready marker; restoration preserves encoding and line endings exactly.</summary>
        public ExecutionEditorWriteFiles()
        {
            ResourceRoot = Path.GetDirectoryName(ModFileService.GetWritablePath(Mod, "manifest.json"));
            Capture("manifest.json");
            Capture(Objects);
            foreach (var file in ModFileService.ListFiles(Mod, "locale", ".properties"))
            {
                Capture(file);
            }

            before = JsonSerializer.Serialize(Snapshot());
        }

        private void Capture(string relative)
        {
            var path = ModFileService.GetWritablePath(Mod, relative);
            originals[relative] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>Includes absent paths and unexpected new locale files so a comparison cannot hide additions or deletions.</summary>
        public Dictionary<string, object> Snapshot()
        {
            var paths = new SortedSet<string>(originals.Keys, StringComparer.Ordinal);
            foreach (var path in ModFileService.ListFiles(Mod, "locale", ".properties"))
            {
                paths.Add(path);
            }

            var files = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var relative in paths)
            {
                var path = ModFileService.GetWritablePath(Mod, relative);
                var bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
                files[relative] = new Dictionary<string, object>
                {
                    ["exists"] = bytes != null,
                    ["bytes"] = bytes?.LongLength ?? 0,
                    ["sha256"] = bytes == null ? null : Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
                };
            }

            return files;
        }

        /// <summary>Restores scoped bytes and clears the same resolver caches used by existing editor tests.</summary>
        public void Restore()
        {
            foreach (var file in ModFileService.ListFiles(Mod, "locale", ".properties"))
            {
                if (!originals.ContainsKey(file))
                {
                    ModFileService.DeleteFile(Mod, file);
                }
            }

            foreach (var pair in originals)
            {
                if (pair.Value == null)
                {
                    ModFileService.DeleteFile(Mod, pair.Key);
                }
                else
                {
                    var path = ModFileService.GetWritablePath(Mod, pair.Key);
                    File.WriteAllBytes(path, pair.Value);
                }
            }

            ModManager.Instance.GetResolver<PatchResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            LocaleFileService.ReloadGame();
            restored = MatchesBefore();
        }

        /// <summary>Compares the complete original scoped byte inventory, including originally absent object output.</summary>
        public bool MatchesBefore()
        {
            return before == JsonSerializer.Serialize(Snapshot());
        }

        /// <summary>Atomic JSON with a full dictionary round-trip check; receipt completion never asserts process exit.</summary>
        public static void Publish(string path, Dictionary<string, object> record)
        {
            var json = JsonSerializer.Serialize(record);
            var roundTrip = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            using var original = JsonDocument.Parse(json);
            if (!JsonElement.DeepEquals(original.RootElement, JsonSerializer.SerializeToElement(roundTrip)))
            {
                throw new InvalidDataException("Editor receipt failed its JSON round trip.");
            }

            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        /// <summary>Restoration remains mandatory when a form assertion, command gate or timeout fails.</summary>
        public void Dispose()
        {
            if (!restored)
            {
                Restore();
            }
        }
    }
}
