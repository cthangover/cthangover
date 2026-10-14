using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Save/load dispatcher for the game core. Serialises game state into
    /// a <see cref="SavePackage"/> (JSON) and drives a <b>three-phase</b>
    /// load with mod metadata validation.
    ///
    /// <b>Architecture:</b> core game state is stored in a reserved
    /// <c>"__core__"</c> slot inside <see cref="SavePackage.ModData"/> —
    /// exactly like any other mod. Other mods add their own slots via
    /// <see cref="IModSaveable"/>. The core never inspects mod blob
    /// payloads; it only routes byte arrays to the owning mod.
    ///
    /// <see cref="RuntimeSerializer"/> handles the actual marshalling
    /// between <see cref="RuntimeData"/> and the
    /// <see cref="DataBlob"/> blocks inside the core slot.
    ///
    /// <b>Save flow:</b>
    /// <list type="number">
    /// <item>Build a <c>"__core__"</c> <see cref="ModSlot"/> via
    ///   <see cref="RuntimeSerializer.BuildCoreSlot"/>.</item>
    /// <item>Call <see cref="ModSaveableRegistry.CollectSaveData"/> —
    ///   each mod fills its own slot. Skip any slot keyed
    ///   <c>"__core__"</c>.</item>
    /// <item>Serialise <see cref="SavePackage"/> to JSON and write to
    ///   disk.</item>
    /// </list>
    ///
    /// <b>Load flow (three phases):</b>
    /// <list type="number">
    /// <item><b>Phase 1 — validation via <see cref="ModMetadata"/>:</b>
    ///   walk all slots, check required mods and core version
    ///   compatibility.</item>
    /// <item><b>Phase 2 — core state restoration:</b>
    ///   <see cref="RuntimeSerializer.RestoreCoreState"/> reads blobs
    ///   from <c>ModData["__core__"]</c> and pushes them into
    ///   <see cref="RuntimeData"/>.</item>
    /// <item><b>Phase 3 —
    ///   <see cref="ModSaveableRegistry.DispatchLoad"/>:</b>
    ///   distribute remaining slots to mods, catch exceptions.</item>
    /// </list>
    /// </summary>
    public static class SaveService
    {
        private const string SaveDir = "user://saves";

        private static string _cachedCoreVersion;
        private static string CoreVersion
        {
            get
            {
                if (_cachedCoreVersion == null)
                {
                    try
                    {
                        _cachedCoreVersion = (string)ProjectSettings.GetSetting("application/config/version");
                    }
                    catch
                    {
                        _cachedCoreVersion = "0.0.0";
                    }
                }
                return _cachedCoreVersion;
            }
        }

        /// <summary>Saves directory: globalised <c>user://saves</c> at runtime, relative fallback otherwise.</summary>
        public static string Path => GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(SaveDir) : "saves";
        
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
            var absDir = Path;
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
        /// Walks the active <see cref="Cthangover.Core.UI.Executable.ExecutableMainEventChain"/>
        /// scene tree node and collects IDs of all one-shot events that
        /// have already executed. These IDs are stored in the save file so
        /// that reloaded games do not re-trigger completed scenarios.
        /// </summary>
        private static HashSet<string> CollectLiveChainOneRunIds()
        {
            var result = new HashSet<string>();
            var tree = (SceneTree)Engine.GetMainLoop();
            if (tree == null)
                return result;

            var chainNode = tree.GetFirstNodeInGroup("main_event_chain");
            var eventChain = chainNode as IExecutableEventChain;
            if (chainNode == null || eventChain == null)
                return result;

            foreach (var child in chainNode.GetChildren())
            {
                if (child is IExecutableEvent evt && evt.IsOneRun && !evt.IsFirstRun)
                    result.Add(evt.ID);
            }
            return result;
        }

        /// <summary>
        /// Merges the two sources of one-run scenario completion state
        /// before writing a save: the persistent per-game history in
        /// <see cref="RuntimeData.CompletedScenarioIds"/> (survives scene
        /// switches, so scenarios completed long ago are not lost when the
        /// player saves from another scene) and IDs of one-run events still
        /// alive in the current event chain. Pure function — safe to call
        /// without a running game tree.
        /// </summary>
        public static HashSet<string> MergeCompletedScenarioIds(IEnumerable<string> liveChainIds, RuntimeData runtime)
        {
            var result = new HashSet<string>();
            if (runtime?.CompletedScenarioIds != null)
                result.UnionWith(runtime.CompletedScenarioIds);
            if (liveChainIds != null)
                result.UnionWith(liveChainIds);
            return result;
        }

        /// <summary>
        /// Builds the full game state into a <see cref="SavePackage"/>,
        /// queries mod data via
        /// <see cref="ModSaveableRegistry.CollectSaveData"/>, and writes
        /// JSON to <c>user://saves/{fileName}.json</c>.
        /// </summary>
        public static void Save(string fileName)
        {
            var gameData = GameData.Instance;
            var pkg = new SavePackage
            {
                SaveId      = fileName,
                Timestamp   = DateTime.UtcNow,
                CoreVersion = CoreVersion,
            };

            pkg.ModData[SavePackage.CoreSlotKey] = RuntimeSerializer.BuildCoreSlot(
                gameData.Runtime,
                CoreVersion,
                GetCurrentSceneName(),
                MergeCompletedScenarioIds(CollectLiveChainOneRunIds(), gameData.Runtime));

            var modSlots = ModSaveableRegistry.CollectSaveData();
            foreach (var kvp in modSlots)
            {
                if (kvp.Key == SavePackage.CoreSlotKey)
                {
                    GameLogger.Log("SAVE",
                        $"Mod uses reserved slot key '__core__', skipping",
                        LogLevel.Warning);
                    continue;
                }
                pkg.ModData[kvp.Key] = kvp.Value;
            }

            EnsureSaveDir();

            try
            {
                string json = JsonSerializer.Serialize(pkg, JsonOptionsProvider.WriteCompactOptions);

                using var file = Godot.FileAccess.Open(GetSavePath(fileName), Godot.FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    GameLogger.Log("SAVE", $"Failed to open save file for writing: {fileName}", LogLevel.Error);
                    return;
                }
                file.StoreString(json);

                GameLogger.Log("SAVE", $"Saved '{fileName}' (core={pkg.CoreVersion}, mods={pkg.ModData.Count})");
            }
            catch (Exception e)
            {
                GameLogger.Log("SAVE", $"Error saving file '{fileName}': {e.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Loads a save file: deserialises the <see cref="SavePackage"/>,
        /// runs a three-phase validation/restore/dispatch.
        /// <list type="number">
        /// <item><b>Phase 1</b> — validate <see cref="ModMetadata"/>:
        ///   required mods must be present, core version must be
        ///   compatible.</item>
        /// <item><b>Phase 2</b> — restore core state via
        ///   <see cref="RuntimeSerializer.RestoreCoreState"/>.</item>
        /// <item><b>Phase 3</b> —
        ///   <see cref="ModSaveableRegistry.DispatchLoad"/>: distribute
        ///   remaining slots to mods.</item>
        /// </list>
        /// Returns <c>true</c> on success.
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
            var pkg = JsonSerializer.Deserialize<SavePackage>(json, JsonOptionsProvider.ReadOptions);

            if (pkg == null)
            {
                GameLogger.Log("SAVE", $"Failed to deserialize save file '{fileName}'", LogLevel.Error);
                return false;
            }

            GameLogger.Log("SAVE", $"Loading '{fileName}' (core={pkg.CoreVersion}, mods={pkg.ModData?.Count ?? 0})");

            // ─── Phase 1: validate mod metadata ───
            if (pkg.ModData != null && pkg.ModData.Count > 0)
            {
                foreach (var kvp in pkg.ModData)
                {
                    if (kvp.Key == SavePackage.CoreSlotKey)
                        continue;

                    var slot = kvp.Value;
                    var meta = slot?.Metadata;
                    if (meta == null || string.IsNullOrEmpty(meta.ModId))
                        continue;

                    var mod = ModManager.Instance.GetMod(meta.ModId);
                    if (mod == null)
                    {
                        if (meta.IsRequired)
                        {
                            GameLogger.Log("SAVE",
                                $"Cannot load save: required mod '{meta.ModId}' ({meta.ModName}) is not installed",
                                LogLevel.Error);
                            return false;
                        }

                        GameLogger.Log("SAVE",
                            $"Mod '{meta.ModId}' ({meta.ModName}) not installed, skipping its data",
                            LogLevel.Warning);
                    }

                    if (!string.IsNullOrEmpty(meta.MinCoreVersion))
                    {
                        try
                        {
                            if (VersionHelper.Compare(CoreVersion, meta.MinCoreVersion) < 0)
                            {
                                GameLogger.Log("SAVE",
                                    $"Cannot load save: mod '{meta.ModId}' ({meta.ModName}) requires core >= {meta.MinCoreVersion}, but current is {CoreVersion}",
                                    LogLevel.Error);
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            GameLogger.Log("SAVE",
                                $"Failed to compare MinCoreVersion '{meta.MinCoreVersion}' for mod '{meta.ModId}': {ex.Message}",
                                LogLevel.Warning);
                        }
                    }

                    if (mod != null && !string.IsNullOrEmpty(meta.ModVersion) && !string.IsNullOrEmpty(mod.Version))
                    {
                        try
                        {
                            var cmp = VersionHelper.Compare(meta.ModVersion, mod.Version);

                            if (cmp > 0)
                            {
                                GameLogger.Log("SAVE",
                                    $"Cannot load save: mod '{meta.ModId}' ({meta.ModName}) " +
                                    $"was saved with version {meta.ModVersion}, but installed version is {mod.Version} " +
                                    $"(downgrade not supported)",
                                    LogLevel.Error);
                                return false;
                            }

                            if (cmp < 0)
                            {
                                GameLogger.Log("SAVE",
                                    $"Mod '{meta.ModId}' ({meta.ModName}) save version {meta.ModVersion} " +
                                    $"is older than installed {mod.Version} — migration will run in OnLoad",
                                    LogLevel.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            GameLogger.Log("SAVE",
                                $"Failed to compare mod version for '{meta.ModId}' " +
                                $"(save={meta.ModVersion}, installed={mod.Version}): {ex.Message}",
                                LogLevel.Warning);
                        }
                    }
                }
            }

            // ─── Phase 2: restore core state ───
            if (pkg.ModData == null || !pkg.ModData.TryGetValue(SavePackage.CoreSlotKey, out var coreSlot))
            {
                GameLogger.Log("SAVE", "Save file is missing '__core__' slot — cannot restore game state", LogLevel.Error);
                return false;
            }
            RuntimeSerializer.RestoreCoreState(coreSlot, GameData.Instance.Runtime);

            // ─── Phase 3: dispatch mod data (everything except __core__) ───
            var modOnlyData = new Dictionary<string, ModSlot>();
            foreach (var kvp in pkg.ModData)
            {
                if (kvp.Key != SavePackage.CoreSlotKey)
                    modOnlyData[kvp.Key] = kvp.Value;
            }

            var modErrors = ModSaveableRegistry.DispatchLoad(modOnlyData);
            if (modErrors.Count > 0)
            {
                foreach (var error in modErrors)
                    GameLogger.Log("SAVE", $"Mod load error: {error}", LogLevel.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Enumerates all existing save slots up to <paramref name="slotCount"/>.
        /// For each slot (<c>slot_1</c> through <c>slot_N</c>), reads the
        /// JSON to extract preview metadata from the <c>"__core__"</c>
        /// slot's <see cref="ModMetadata.CustomData"/> for the load-game UI.
        /// Empty slots (no file on disk) are represented by
        /// <see cref="SaveSlotInfo"/> instances with
        /// <see cref="SaveSlotInfo.IsEmpty"/> = <c>true</c>.
        /// </summary>
        public static List<SaveSlotInfo> GetSaveSlots(int slotCount)
        {
            var result = new List<SaveSlotInfo>();
            var absDir = Path;

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
                                    var pkg = JsonSerializer.Deserialize<SavePackage>(json, JsonOptionsProvider.ReadOptions);

                                    var preview = new RuntimeSerializer.CoreSlotPreview();
                                    if (pkg?.ModData != null
                                        && pkg.ModData.TryGetValue(SavePackage.CoreSlotKey, out var coreSlot))
                                    {
                                        preview = RuntimeSerializer.ReadSlotPreview(coreSlot.Metadata);
                                    }

                                    var info = new SaveSlotInfo
                                    {
                                        FileName = slotName,
                                        SaveTime = pkg?.Timestamp ?? DateTime.MinValue,
                                        SceneName = preview.CurrentSceneName ?? "unknown",
                                        HasScreenshot = File.Exists(
                                            ProjectSettings.GlobalizePath(GetScreenshotPath(slotName))),
                                        IsEmpty = false,
                                        ScreenshotPath = GetScreenshotPath(slotName),
                                        GameTime = preview.GameTime,
                                        CharacterCount = preview.CharacterCount,
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
