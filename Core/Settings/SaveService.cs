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
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{

    public static class SaveService
    {
        private const string SaveDir = "user://saves";

        private static string GetSavePath(string fileName)
        {
            var safe = new string(fileName.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            return $"{SaveDir}/{safe}.json";
        }

        private static string GetScreenshotPath(string fileName)
        {
            var safe = new string(fileName.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            return $"{SaveDir}/{safe}.png";
        }

        private static void EnsureSaveDir()
        {
            var absDir = ProjectSettings.GlobalizePath(SaveDir);
            if (!DirAccess.DirExistsAbsolute(absDir))
                DirAccess.MakeDirRecursiveAbsolute(absDir);
        }

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
                CurrentSceneName = GetCurrentSceneName(),
                SaveDateTime = DateTime.UtcNow,
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
                saveData.BattleSet?.ToHashSet() ?? new HashSet<CharacterType>();

            runtime.CharacterData.Characters =
                new Dictionary<CharacterType, CharacterInfoData>();
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

            QuestFactory.Instance.SetAll(saveData.Quests);

            return true;
        }

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
