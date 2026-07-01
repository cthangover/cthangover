using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Quests;
using Cthangover.Core.Relationship;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Static facade for the save/load subsystem operating on
    /// <c>user://saves/</c>. Serializes the entire <see cref="RuntimeData"/>
    /// subgraph into a flat <see cref="SaveData"/> DTO and writes it as
    /// compact JSON. On load, it reconstructs each subsystem (time, lamp,
    /// characters, inventory, quests, recipes, recruiting, battle set,
    /// completed scenario IDs) by calling the appropriate setters.
    /// Slot enumeration (<see cref="GetSaveSlots"/>) reads header-level
    /// data from every JSON file without deserializing the full payload.
    /// </summary>
    public static class SaveService
    {
        private const string SaveDir = "user://saves";

        /// <summary>
        /// Builds a sanitised file path for a save slot. Non-alphanumeric
        /// characters (except <c>_</c> and <c>-</c>) are replaced with <c>_</c>
        /// to prevent path traversal.
        /// </summary>
        private static string GetSavePath(string fileName)
        {
            var safe = new string(fileName.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            return $"{SaveDir}/{safe}.json";
        }

        /// <summary>
        /// Walks the active <see cref="Cthangover.Core.UI.Executable.ExecutableMainEventChain"/>
        /// scene tree node and collects IDs of all one-shot events that
        /// have already executed. These IDs are stored in the save file so
        /// that reloaded games do not re-trigger completed scenarios.
        /// </summary>
        private static HashSet<string> CollectCompletedScenarioIds()
        {
            var result = new HashSet<string>();
            var tree = (SceneTree)Engine.GetMainLoop();
            if (tree == null)
                return result;

            var eventChain = tree.GetFirstNodeInGroup("main_event_chain") as ExecutableMainEventChain;
            if (eventChain == null)
                return result;

            foreach (var child in eventChain.GetChildren())
            {
                if (child is ExecutableEvent evt && evt.IsOneRun && !evt.IsFirstRun)
                    result.Add(evt.ID);
            }
            return result;
        }

        /// <summary>
        /// Builds the PNG screenshot path for a save slot using the same
        /// sanitisation rules as <see cref="GetSavePath"/>.
        /// </summary>
        private static string GetScreenshotPath(string fileName)
        {
            var safe = new string(fileName.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            return $"{SaveDir}/{safe}.png";
        }

        /// <summary>
        /// Ensures <c>user://saves/</c> directory exists, creating it
        /// recursively if needed.
        /// </summary>
        private static void EnsureSaveDir()
        {
            var absDir = ProjectSettings.GlobalizePath(SaveDir);
            if (!DirAccess.DirExistsAbsolute(absDir))
                DirAccess.MakeDirRecursiveAbsolute(absDir);
        }

        /// <summary>
        /// Resolves the currently active scene name by querying the
        /// <see cref="Cthangover.Core.Scenes.SceneManager"/> singleton
        /// first, then falling back to the current scene's filename.
        /// Returns "unknown" if neither source is available.
        /// </summary>
        private static string GetCurrentSceneName()
        {
            var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
            if (sceneManager != null && !string.IsNullOrEmpty(sceneManager.CurrentSceneName))
                return sceneManager.CurrentSceneName;

            var scene = SceneContextNode.CurrentScene;
            if (scene != null)
            {
                var path = scene.SceneFilePath;
                if (!string.IsNullOrEmpty(path))
                    return System.IO.Path.GetFileNameWithoutExtension(path);
            }

            return "unknown";
        }

        /// <summary>
        /// Captures the full game state into a <see cref="SaveData"/> DTO
        /// and writes it as JSON to <c>user://saves/{fileName}.json</c>.
        /// Also persists a screenshot via <see cref="SaveScreenshotService"/>.
        /// All subsystems are sampled synchronously: time, lamp, characters,
        /// quests, recruiting, battle set, inventory, recipes, and completed
        /// scenario IDs. Write failures are logged but do not throw.
        /// </summary>
        public static void Save(string fileName)
        {
            var gameData = GameData.Instance;
            var saveData = new SaveData
            {
                Time       = gameData.Runtime.Time.Tick,
                LampRadius = gameData.Runtime.LampData.Radius,
                LampInfluence = gameData.Runtime.LampData.Influence,
                Characters = gameData.Runtime.CharacterData.Characters.Values.ToList(),
                Quests     = QuestFactory.Instance.GetAll(),
                Recruits     = gameData.Runtime.RecruitingData.Data,
                BattleSet  = gameData.Runtime.CharacterData.BattleSet.ToList(),
                Inventory  = gameData.Runtime.Inventory.Items.Select(o =>
                    new CItem { ID = o.Item.ID, Count = o.Count }).ToList(),
                Recipes    = gameData.Runtime.RecipeData.Data.ToList(),
                ActionPool  = gameData.Runtime.ActionPool.ActionIds.ToList(),
                CurrentSceneName = GetCurrentSceneName(),
                SaveDateTime = DateTime.UtcNow,
                GameTime = gameData.Runtime.Time.Tick,
                CharacterCount = gameData.Runtime.CharacterData.Characters?.Count ?? 0,
                CompletedScenarioIds = CollectCompletedScenarioIds(),
            };

            EnsureSaveDir();

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = false };
                string json = JsonSerializer.Serialize(saveData, options);

                using var file = Godot.FileAccess.Open(GetSavePath(fileName), Godot.FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    GameLogger.Log("SAVE", $"Failed to open save file for writing: {fileName}", LogLevel.Error);
                    return;
                }
                file.StoreString(json);
            }
            catch (Exception e)
            {
                GameLogger.Log("SAVE", $"Error saving file '{fileName}': {e.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Deserializes a save file and pushes every subsystem value back
        /// into <see cref="RuntimeData"/>. Returns <c>true</c> on success.
        /// Character data is rebuilt as a dictionary keyed by
        /// <see cref="CharacterInfoData.CharacterType"/>; inventory items
        /// are rehydrated from <see cref="CItem"/> into full
        /// <see cref="Cthangover.Core.Items.IItemContainer"/> objects via
        /// <see cref="Cthangover.Core.Factories.Impls.ItemFactory"/>.
        /// Completed scenario IDs are stored in
        /// <see cref="RuntimeData.LoadedCompletedScenarioIds"/> for
        /// post-load processing.
        /// </summary>
        public static bool Load(string fileName)
        {
            var path = GetSavePath(fileName);
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GameLogger.Log("SAVE", $"Save file not found: {fileName}", LogLevel.Error);
                return false;
            }

            var json = file.GetAsText();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var saveData = JsonSerializer.Deserialize<SaveData>(json, options);

            if (saveData == null)
            {
                GameLogger.Log("SAVE", $"Failed to deserialize save file '{fileName}'", LogLevel.Error);
                return false;
            }

            var runtime = GameData.Instance.Runtime;

            runtime.Time.SetTime(saveData.Time);
            runtime.LampData.Radius = saveData.LampRadius;
            runtime.LampData.Influence = saveData.LampInfluence;
            runtime.RecruitingData.Data = saveData.Recruits ?? new List<Recruit>();
            runtime.CharacterData.BattleSet =
                saveData.BattleSet?.ToHashSet() ?? new HashSet<string>();

            runtime.CharacterData.Characters =
                new Dictionary<string, CharacterInfoData>();
            if (saveData.Characters != null)
            {
                foreach (var character in saveData.Characters)
                    runtime.CharacterData.Characters[character.CharacterType] = character;
            }

            runtime.Inventory.Items =
                saveData.Inventory?.Select(o => (IItemContainer)new ItemContainer
                    { Item = ItemFactory.Instance.Get(o.ID), Count = o.Count }).ToList()
                ?? new List<IItemContainer>();

            runtime.RecipeData.Data = saveData.Recipes?.ToHashSet() ?? new HashSet<string>();

            runtime.ActionPool.ActionIds = saveData.ActionPool ?? new List<string>();

            QuestFactory.Instance.SetAll(saveData.Quests);

            runtime.LoadedCompletedScenarioIds = saveData.CompletedScenarioIds ?? new HashSet<string>();

            return true;
        }

        /// <summary>
        /// Enumerates all existing save slots up to <paramref name="slotCount"/>.
        /// For each slot (<c>slot_1</c> through <c>slot_N</c>), reads the
        /// JSON header to extract metadata for the load-game UI. Empty slots
        /// (no file on disk) are represented by <see cref="SaveSlotInfo"/>
        /// instances with <see cref="SaveSlotInfo.IsEmpty"/> = <c>true</c>,
        /// so the UI always has a fixed-size grid.
        /// </summary>
        public static List<SaveSlotInfo> GetSaveSlots(int slotCount)
        {
            var result = new List<SaveSlotInfo>();
            var absDir = ProjectSettings.GlobalizePath(SaveDir);

            var existingSaves = new Dictionary<string, SaveSlotInfo>();

            if (DirAccess.DirExistsAbsolute(absDir))
            {
                var dir = DirAccess.Open(SaveDir);
                if (dir != null)
                {
                    dir.ListDirBegin();
                    var fileName = dir.GetNext();
                    while (!string.IsNullOrEmpty(fileName))
                    {
                        if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                        {
                            var slotName = fileName.Substring(0, fileName.Length - 5);
                            try
                            {
                                using var file = Godot.FileAccess.Open($"{SaveDir}/{fileName}", Godot.FileAccess.ModeFlags.Read);
                                if (file != null)
                                {
                                    var json = file.GetAsText();
                                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                    var saveData = JsonSerializer.Deserialize<SaveData>(json, options);

                                    var info = new SaveSlotInfo
                                    {
                                        FileName = slotName,
                                        SaveTime = saveData?.SaveDateTime ?? DateTime.MinValue,
                                        SceneName = saveData?.CurrentSceneName ?? "unknown",
                                        HasScreenshot = File.Exists(
                                            ProjectSettings.GlobalizePath(GetScreenshotPath(slotName))),
                                        IsEmpty = false,
                                        ScreenshotPath = GetScreenshotPath(slotName),
                                        GameTime = saveData?.GameTime ?? 0,
                                        CharacterCount = saveData?.CharacterCount ?? 0,
                                        LampPercent = 0f,
                                    };

                                    existingSaves[slotName] = info;
                                }
                            }
                            catch
                            {
                            }
                        }

                        fileName = dir.GetNext();
                    }

                    dir.ListDirEnd();
                }
            }

            for (int i = 0; i < slotCount; i++)
            {
                var slotName = $"slot_{i + 1}";
                if (existingSaves.TryGetValue(slotName, out var info))
                {
                    result.Add(info);
                }
                else
                {
                    result.Add(new SaveSlotInfo
                    {
                        FileName = slotName,
                        SaveTime = DateTime.MinValue,
                        SceneName = "",
                        HasScreenshot = false,
                        IsEmpty = true,
                        ScreenshotPath = GetScreenshotPath(slotName),
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Deletes both the JSON save file and its associated PNG screenshot
        /// for the given slot. Failures (e.g. file already missing) are logged
        /// but do not throw.
        /// </summary>
        public static void DeleteSave(string fileName)
        {
            var jsonPath = ProjectSettings.GlobalizePath(GetSavePath(fileName));
            var pngPath = ProjectSettings.GlobalizePath(GetScreenshotPath(fileName));

            try
            {
                if (File.Exists(jsonPath))
                    File.Delete(jsonPath);
                if (File.Exists(pngPath))
                    File.Delete(pngPath);
            }
            catch (Exception e)
            {
                GameLogger.Log("SAVE", $"Error deleting save '{fileName}': {e.Message}", LogLevel.Error);
            }
        }
    }
}
