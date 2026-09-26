using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Persists user-defined mod load order to <c>user://mod_load_order.json</c>.
    /// The stored list is a preference that is merged with mandatory dependency
    /// ordering from <c>ModManifest.Depends</c> — dependencies always override
    /// manual ordering. New mods not present in the stored list are inserted
    /// automatically at their alphabetically-appropriate position.
    /// </summary>
    public static class ModLoadOrderStore
    {
        private const string FilePath = "user://mod_load_order.json";

        /// <summary>
        /// Serialises file access. <c>user://mod_load_order.json</c> can be
        /// written from multiple contexts (mod bootstrap, save loading,
        /// parallel unit tests); unsynchronised <see cref="Save"/> calls
        /// throw <see cref="IOException"/> ("file in use by another process").
        /// </summary>
        private static readonly object _fileLock = new();

        public static string Path => GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(FilePath) : "mod_load_order.json";
        
        public static List<string> Load()
        {
            var path = Path;
            if (!File.Exists(path))
                return new List<string>();

            try
            {
                lock (_fileLock)
                {
                    var json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptionsProvider.ReadOptions);
                    return list ?? new List<string>();
                }
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void Save(List<string> orderedIds)
        {
            var path = Path;
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(orderedIds, JsonOptionsProvider.WriteCompactOptions);
            lock (_fileLock)
            {
                File.WriteAllText(path, json);
            }
        }

        /// <summary>
        /// Moves a mod up by one position in the saved order.
        /// If the mod is not in the list, it is appended first.
        /// </summary>
        public static void MoveUp(string modId)
        {
            var order = Load();
            var index = order.IndexOf(modId);
            if (index < 0)
            {
                order.Add(modId);
                Save(order);
                return;
            }
            if (index == 0)
                return;
            (order[index - 1], order[index]) = (order[index], order[index - 1]);
            Save(order);
        }

        /// <summary>
        /// Moves a mod down by one position in the saved order.
        /// If the mod is not in the list, it is appended first.
        /// </summary>
        public static void MoveDown(string modId)
        {
            var order = Load();
            var index = order.IndexOf(modId);
            if (index < 0)
            {
                order.Add(modId);
                Save(order);
                return;
            }
            if (index >= order.Count - 1)
                return;
            (order[index + 1], order[index]) = (order[index], order[index + 1]);
            Save(order);
        }

        /// <summary>
        /// Returns the existing user-defined manual order, removing IDs
        /// for mods that no longer exist. New mods (not in the saved order)
        /// are NOT added — they will be placed by alphabetical order in
        /// <see cref="ModLoadOrder.Compute"/>. Does NOT save to disk.
        /// </summary>
        public static List<string> Sync(List<string> allModIds)
        {
            var order = Load();
            order.RemoveAll(id => !allModIds.Contains(id));
            return order;
        }

        /// <summary>
        /// Removes a mod id from the saved manual order and persists the change.
        /// Called by <see cref="ModUninstaller"/> after the mod's files are gone.
        /// </summary>
        public static void Remove(string modId)
        {
            var order = Load();
            if (order.Remove(modId))
                Save(order);
        }
    }
}
