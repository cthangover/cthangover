using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Registry of <see cref="IModSettings"/> implementations. When a
    /// mod assembly is loaded, types implementing the interface are
    /// scanned and instantiated via reflection, mirroring the pattern
    /// used by <see cref="ModSaveableRegistry"/>.
    ///
    /// Persistence: each mod's settings are stored as a JSON-serialised
    /// <see cref="DataBlob"/> at <c>user://mod_settings/{modId}.json</c>.
    /// The core owns persistence entirely; the mod only defines settings
    /// and reads/writes values through the blob API.
    ///
    /// <b>Thread safety:</b> all I/O is synchronous on the main thread.
    /// </summary>
    public static class ModSettingsRegistry
    {
        private const string SettingsDir = "user://mod_settings";

        private static readonly List<RegisteredModSettings> _entries = new();

        private class RegisteredModSettings
        {
            public IModSettings Instance;
            public string ModId;
        }

        /// <summary>
        /// Discovers <see cref="IModSettings"/> implementations in the
        /// assembly and registers them under the given modId.
        /// </summary>
        public static void RegisterAssembly(Assembly assembly, string modId)
        {
            foreach (var instance in Reflections.FindAndCreate<IModSettings>(assembly))
            {
                try
                {
                    _entries.Add(new RegisteredModSettings { Instance = instance, ModId = modId });
                    GameLogger.Log("MODS_SETTINGS", $"Registered IModSettings '{instance.GetType().FullName}' for mod '{modId}'", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_SETTINGS", $"Failed to create IModSettings '{instance.GetType().FullName}': {ex.Message}", LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// Removes every settings entry registered for <paramref name="modId"/>
        /// from the given assembly — the unload pass of the assembly consumer
        /// pipeline. Persisted settings on disk are intentionally kept: a
        /// reinstalled mod picks its values back up.
        /// </summary>
        public static void UnregisterAssembly(Assembly assembly, string modId)
        {
            _entries.RemoveAll(e => e.ModId == modId && e.Instance.GetType().Assembly == assembly);
        }

        /// <summary>
        /// Returns the mod IDs of all registered mods that implement
        /// <see cref="IModSettings"/>, sorted by their display title.
        /// </summary>
        public static IReadOnlyList<string> GetModsWithSettings()
        {
            return _entries
                .Select(e => e.ModId)
                .Distinct()
                .OrderBy(id => GetDisplayTitle(id))
                .ToList();
        }

        /// <summary>
        /// Returns the human-readable display title for a mod, falling
        /// back to the mod ID if the mod is not found.
        /// </summary>
        public static string GetDisplayTitle(string modId)
        {
            var mod = ModRegistry.Instance.GetMod(modId);
            return mod?.DisplayTitle ?? modId;
        }

        /// <summary>
        /// Returns setting descriptors for the given mod. Returns an
        /// empty list if the mod has no settings registered.
        /// </summary>
        public static IReadOnlyList<ModSettingDefinition> GetDefinitions(string modId)
        {
            var entry = _entries.Find(e => e.ModId == modId);
            if (entry == null)
                return Array.Empty<ModSettingDefinition>();

            try
            {
                return entry.Instance.GetDefinitions() ?? Array.Empty<ModSettingDefinition>();
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_SETTINGS", $"GetDefinitions failed for mod '{modId}': {ex.Message}", LogLevel.Error);
                return Array.Empty<ModSettingDefinition>();
            }
        }

        /// <summary>
        /// Reads the mod's current values into a <see cref="DataBlob"/>.
        /// Returns an empty blob on failure.
        /// </summary>
        public static DataBlob WriteCurrentValues(string modId)
        {
            var entry = _entries.Find(e => e.ModId == modId);
            if (entry == null)
                return new DataBlob();

            try
            {
                var blob = new DataBlob();
                entry.Instance.WriteValues(blob);
                return blob;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_SETTINGS", $"WriteValues failed for mod '{modId}': {ex.Message}", LogLevel.Error);
                return new DataBlob();
            }
        }

        /// <summary>
        /// Applies values from a <see cref="DataBlob"/> to the mod.
        /// </summary>
        public static void ApplyValues(string modId, DataBlob blob)
        {
            var entry = _entries.Find(e => e.ModId == modId);
            if (entry == null || blob == null)
                return;

            try
            {
                entry.Instance.ReadValues(blob);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_SETTINGS", $"ReadValues failed for mod '{modId}': {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads persisted settings for a single mod from disk.
        /// Returns a <see cref="DataBlob"/> with the saved values,
        /// or an empty blob if no file exists.
        /// </summary>
        public static DataBlob LoadModSettings(string modId)
        {
            var path = GetSettingsPath(modId);

            if (GameEnvironment.IsRuntime)
            {
                if (!Godot.FileAccess.FileExists(path))
                    return new DataBlob();
            }
            else
            {
                if (!File.Exists(path))
                    return new DataBlob();
            }

            try
            {
                string json;
                if (GameEnvironment.IsRuntime)
                {
                    using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                    json = file.GetAsText();
                }
                else
                {
                    json = File.ReadAllText(path);
                }

                if (string.IsNullOrEmpty(json))
                    return new DataBlob();

                var payload = Encoding.UTF8.GetBytes(json);
                return new DataBlob(payload);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_SETTINGS", $"Failed to load settings for mod '{modId}': {ex.Message}", LogLevel.Error);
                return new DataBlob();
            }
        }

        /// <summary>
        /// Persists a <see cref="DataBlob"/> to disk for a single mod.
        /// </summary>
        public static void SaveModSettings(string modId, DataBlob blob)
        {
            if (blob == null)
                return;

            EnsureDirectoryExists();

            var path = GetSettingsPath(modId);

            try
            {
                var json = Encoding.UTF8.GetString(blob.GetBytes());
                if (GameEnvironment.IsRuntime)
                {
                    using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
                    file.StoreString(json);
                }
                else
                {
                    File.WriteAllText(path, json);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_SETTINGS", $"Failed to save settings for mod '{modId}': {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Deletes persisted settings for a single mod (used by the
        /// Reset button). After deletion the mod keeps its current
        /// in-memory values — the user must reopen settings to see
        /// defaults.
        /// </summary>
        public static void DeleteModSettings(string modId)
        {
            var path = GetSettingsPath(modId);

            try
            {
                if (GameEnvironment.IsRuntime)
                {
                    if (Godot.FileAccess.FileExists(path))
                        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
                }
                else
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_SETTINGS", $"Failed to delete settings for mod '{modId}': {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads persisted settings for every registered mod and
        /// applies them via <see cref="IModSettings.ReadValues"/>.
        /// Called once at boot, after all mods have been loaded.
        /// </summary>
        public static void LoadAll()
        {
            if (_entries.Count == 0)
                return;

            EnsureDirectoryExists();

            foreach (var entry in _entries)
            {
                var blob = LoadModSettings(entry.ModId);
                if (blob.GetBytes().Length == 0)
                    continue;

                try
                {
                    entry.Instance.ReadValues(blob);
                    GameLogger.Log("MODS_SETTINGS", $"Loaded settings for mod '{entry.ModId}'");
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MODS_SETTINGS", $"ReadValues failed during LoadAll for mod '{entry.ModId}': {ex.Message}", LogLevel.Error);
                }
            }
        }

        private static string GetSettingsPath(string modId)
        {
            var safe = new string(modId.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            var fileName = $"{safe}.json";
            if (GameEnvironment.IsRuntime)
                return Path.Combine(SettingsDir, fileName);
            return Path.Combine("mod_settings", fileName);
        }

        private static void EnsureDirectoryExists()
        {
            if (GameEnvironment.IsRuntime)
            {
                var abs = ProjectSettings.GlobalizePath(SettingsDir);
                if (!Directory.Exists(abs))
                    Directory.CreateDirectory(abs);
            }
            else
            {
                if (!Directory.Exists("mod_settings"))
                    Directory.CreateDirectory("mod_settings");
            }
        }
    }
}
