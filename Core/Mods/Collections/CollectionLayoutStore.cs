using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Loads and persists scheme layouts from <c>user://collection_layout.json</c>.
    /// The file holds one <see cref="ModCollectionLayout"/> per game id, so the
    /// player's arrangement of the "РПГ Новелла" canvas is independent from any
    /// other assembly. Layouts are UI-only: if the file is missing or broken the
    /// editor simply falls back to its default packed layout, and nothing in the
    /// collection documents is affected.
    /// <para/>
    /// Follows the <see cref="ModCollectionStore"/> warm-up pattern: the user
    /// path is resolved once on the main thread because
    /// <c>ProjectSettings.GlobalizePath</c> is Godot API and is not available
    /// from worker threads.
    /// </summary>
    public static class CollectionLayoutStore
    {
        private const string UserFilePath = "user://collection_layout.json";

        private static Dictionary<string, ModCollectionLayout> _cache;
        private static string _resolvedPath;

        /// <summary>
        /// Resolves the user storage path once on the main thread before the
        /// background mod-load phase, mirroring <see cref="ModCollectionStore"/>.
        /// </summary>
        public static void WarmUp()
        {
            _resolvedPath = GameEnvironment.IsRuntime
                ? ProjectSettings.GlobalizePath(UserFilePath)
                : "collection_layout.json";
        }

        private static string ResolvedPath =>
            _resolvedPath ?? (GameEnvironment.IsRuntime
                ? ProjectSettings.GlobalizePath(UserFilePath)
                : "collection_layout.json");

        /// <summary>Returns the saved layout for a game, or a fresh empty one. Never null.</summary>
        public static ModCollectionLayout Load(string gameId)
        {
            var all = LoadAll();
            if (!string.IsNullOrEmpty(gameId) && all.TryGetValue(gameId, out var layout) && layout != null)
                return layout;

            return new ModCollectionLayout();
        }

        /// <summary>Stores the layout for a game and flushes the whole file.</summary>
        public static void Save(string gameId, ModCollectionLayout layout)
        {
            if (string.IsNullOrEmpty(gameId) || layout == null)
                return;

            var all = LoadAll();
            all[gameId] = layout;
            SaveAll(all);
        }

        private static Dictionary<string, ModCollectionLayout> LoadAll()
        {
            if (_cache != null)
                return _cache;

            _cache = new Dictionary<string, ModCollectionLayout>(StringComparer.OrdinalIgnoreCase);

            var path = ResolvedPath;
            if (!File.Exists(path))
                return _cache;

            try
            {
                var json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, ModCollectionLayout>>(
                    json, JsonOptionsProvider.ReadOptions);
                if (parsed != null)
                    _cache = new Dictionary<string, ModCollectionLayout>(parsed, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Failed to load collection layouts: {ex.Message}", LogLevel.Warning);
            }

            return _cache;
        }

        private static void SaveAll(Dictionary<string, ModCollectionLayout> all)
        {
            try
            {
                var path = ResolvedPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(all, JsonOptionsProvider.WriteIndentedIgnoreNullOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Failed to save collection layouts: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
