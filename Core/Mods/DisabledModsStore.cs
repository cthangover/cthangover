using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Persistent storage for disabled mod IDs in <c>user://disabled_mods.json</c>.
    /// Used by <see cref="ModRegistry"/> to filter out disabled mods during
    /// loading and by the mod manager screen to display and toggle mod states.
    /// The file is a simple JSON array of mod ID strings.
    /// </summary>
    public static class DisabledModsStore
    {
        private const string FilePath = "user://disabled_mods.json";

        private static string _resolvedPath;

        /// <summary>
        /// Resolves the storage path once on the main thread. Called from
        /// <see cref="ModRegistry.PreparePaths"/> before the background mod-load
        /// phase, because <c>ProjectSettings.GlobalizePath</c> is Godot API and
        /// cannot be called from a worker thread.
        /// </summary>
        public static void WarmUp()
        {
            _resolvedPath = GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(FilePath) : "disabled_mods.json";
        }

        private static string ResolvedPath =>
            _resolvedPath ?? (GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(FilePath) : "disabled_mods.json");

        /// <summary>Reads the disabled-mod id set; returns an empty set when the file is absent or broken.</summary>
        public static HashSet<string> Load()
        {
            var path = ResolvedPath;
            if (!File.Exists(path))
                return new HashSet<string>();

            try
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptionsProvider.ReadOptions);
                return list != null ? new HashSet<string>(list) : new HashSet<string>();
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        /// <summary>Persists the disabled-mod id set as a sorted JSON array.</summary>
        public static void Save(HashSet<string> disabledIds)
        {
            var path = ResolvedPath;
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var list = new List<string>(disabledIds);
            list.Sort();
            var json = JsonSerializer.Serialize(list, JsonOptionsProvider.WriteCompactOptions);
            File.WriteAllText(path, json);
        }

        /// <summary>Convenience check backed by <see cref="Load"/>; allocates a fresh set per call.</summary>
        public static bool IsDisabled(string modId)
        {
            return Load().Contains(modId);
        }

        /// <summary>Adds or removes a single id and persists the change; used by the mod manager toggle.</summary>
        public static void SetEnabled(string modId, bool enabled)
        {
            var set = Load();
            if (enabled)
                set.Remove(modId);
            else
                set.Add(modId);
            Save(set);
        }

        /// <summary>
        /// Forgets a mod id entirely. Called by <see cref="ModUninstaller"/> so a
        /// deleted mod does not leave a stale entry (which would otherwise disable
        /// a re-installed mod of the same id).
        /// </summary>
        public static void Remove(string modId)
        {
            var set = Load();
            if (set.Remove(modId))
                Save(set);
        }
    }
}
