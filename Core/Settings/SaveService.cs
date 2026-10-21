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
        private const string SavesRoot = "user://saves";

        /// <summary>
        /// Saves of the active game live in <c>user://saves/&lt;game id&gt;/</c>,
        /// so editing the game's mechanics keeps them; without an active game
        /// (development launches with every mod) the shared root is used.
        /// </summary>
        private static string SaveDir => Games.ActiveGame.IsActive
            ? $"{SavesRoot}/{Sanitize(Games.ActiveGame.Id)}"
            : SavesRoot;

        /// <summary>Slots carried forward for mods the current game runs without; see <see cref="SaveDormancy"/>.</summary>
        private static Dictionary<string, ModSlot> _dormant = new();

        /// <summary>Scene stored in the last successfully loaded save (null before any load).</summary>
        public static string LastLoadedSceneName { get; private set; }

        /// <summary>Composition changes found by the last load (mods that went dormant or came back).</summary>
        public static SaveDormancy.Result LastLoadDormancy { get; private set; }

        /// <summary>Forgets dormant slots; called when a new game starts.</summary>
        public static void ClearDormant() => _dormant = new Dictionary<string, ModSlot>();

        /// <summary>Directory of the active game's saves (Godot path).</summary>
        public static string SaveDirectory => SaveDir;

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
            => $"{SaveDir}/{Sanitize(fileName)}.json";

        private static string Sanitize(string name)
            => new string((name ?? string.Empty).Select(c =>
                char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());

        /// <summary>
        /// Builds the PNG screenshot path for a save slot using the same
        /// sanitisation rules as <see cref="GetSavePath"/>.
        /// </summary>
        private static string GetScreenshotPath(string fileName)
            => $"{SaveDir}/{Sanitize(fileName)}.png";

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
        public static void Save(string fileName) => Save(fileName, rememberSession: true);

        /// <summary>
        /// Prefix of technical saves (<see cref="HotReloadSlot"/>): written and
        /// read like any slot, but hidden from the save browser and never
        /// recorded as the "Continue" session.
        /// </summary>
        public const string HiddenSlotPrefix = "__";

        /// <summary>The slot a hot reload parks the running session in while the mod set is replaced.</summary>
        public const string HotReloadSlot = HiddenSlotPrefix + "hot_reload";

        /// <summary>
        /// <see cref="Save(string)"/> with control over the "Continue" record:
        /// technical saves pass <paramref name="rememberSession"/> = false so the
        /// player's last real save stays the one "Continue" loads.
        /// </summary>
        public static void Save(string fileName, bool rememberSession)
        {
            var pkg = BuildPackage(fileName);

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
                if (rememberSession)
                    LastSessionStore.Record(Games.ActiveGame.Id, fileName);
            }
            catch (Exception e)
            {
                GameLogger.Log("SAVE", $"Error saving file '{fileName}': {e.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Collects the complete state of the running game into a package — the
        /// core slot, every <see cref="IModSaveable"/> slot, the game info and the
        /// dormant slots — without touching disk. <see cref="Save"/> writes it to
        /// a slot file; tools keep it in memory as a snapshot and bring it back
        /// with <see cref="ApplyPackage"/> (the scenario studio rolls a scenario
        /// back to its start this way).
        /// </summary>
        public static SavePackage BuildPackage(string saveId)
        {
            var gameData = GameData.Instance;
            var pkg = new SavePackage
            {
                SaveId      = saveId,
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

            pkg.Game = BuildGameInfo();
            pkg.Dormant = _dormant.Count > 0 ? new Dictionary<string, ModSlot>(_dormant) : null;
            return pkg;
        }

        /// <summary>
        /// Loads a save file of the active game. The game may have changed its
        /// mechanics since the save was written, so the load adapts instead of
        /// refusing:
        /// <list type="number">
        /// <item><b>Phase 1</b> — <see cref="SaveDormancy.Classify"/>: slots of
        ///   mods that are not loaded, older than the save or need a newer core
        ///   become dormant; dormant slots whose mod is back are revived.</item>
        /// <item><b>Phase 2</b> — reset every state owner, then restore core
        ///   state via <see cref="RuntimeSerializer.RestoreCoreState"/>.</item>
        /// <item><b>Phase 3</b> — <see cref="ModSaveableRegistry.DispatchLoad"/>;
        ///   a rejected slot is kept dormant; mods without a slot get
        ///   <see cref="IModSaveable.OnJoinedExistingGame"/>.</item>
        /// </list>
        /// Only a missing core slot fails the load.
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

            SavePackage pkg;
            try
            {
                pkg = JsonSerializer.Deserialize<SavePackage>(file.GetAsText(), JsonOptionsProvider.ReadOptions);
            }
            catch (JsonException ex)
            {
                // A slot cut short by a crash or a full disk: report it, the menu stays usable.
                GameLogger.Log("SAVE", $"Save file '{fileName}' is damaged: {ex.Message}", LogLevel.Error);
                return false;
            }

            if (pkg == null)
            {
                GameLogger.Log("SAVE", $"Failed to deserialize save file '{fileName}'", LogLevel.Error);
                return false;
            }

            if (!ApplyPackage(pkg, fileName))
                return false;

            LastSessionStore.Record(Games.ActiveGame.Id, fileName);
            return true;
        }

        /// <summary>
        /// Restores the game state from a package built by <see cref="BuildPackage"/>
        /// or read from a slot file — the three phases described on <see cref="Load"/>.
        /// Does not touch the last-session record; <paramref name="sourceName"/> is
        /// only used in the log. The package's slots may be reused by the restore,
        /// so a snapshot that is applied repeatedly should be kept serialized and
        /// deserialized per apply.
        /// </summary>
        public static bool ApplyPackage(SavePackage pkg, string sourceName)
        {
            var fileName = sourceName;
            GameLogger.Log("SAVE", $"Loading '{fileName}' (core={pkg.CoreVersion}, mods={pkg.ModData?.Count ?? 0})");

            // ─── Phase 1: decide which slots the current composition restores ───
            // Nothing blocks the load except a missing core slot: slots of mods
            // the game runs without stay dormant and come back with the mod.
            if (pkg.ModData == null || !pkg.ModData.TryGetValue(SavePackage.CoreSlotKey, out var coreSlot))
            {
                GameLogger.Log("SAVE", "Save file is missing '__core__' slot — cannot restore game state", LogLevel.Error);
                return false;
            }

            if (pkg.Game != null && Games.ActiveGame.IsActive
                && !string.Equals(pkg.Game.Id, Games.ActiveGame.Id, StringComparison.OrdinalIgnoreCase))
            {
                GameLogger.Log("SAVE",
                    $"Save '{fileName}' belongs to game '{pkg.Game.Id}', loading it into '{Games.ActiveGame.Id}'",
                    LogLevel.Warning);
            }

            var dormancy = SaveDormancy.Classify(
                pkg.ModData,
                pkg.Dormant,
                id => ModManager.Instance.GetMod(id) is { } mod ? mod.Version ?? "0.0.0" : null,
                CoreVersion,
                DateTime.UtcNow);

            foreach (var (modId, reason) in dormancy.NewlyDormant)
                GameLogger.Log("SAVE", $"Mod '{modId}' data kept dormant ({reason}) — it returns when the mod is back", LogLevel.Warning);
            foreach (var modId in dormancy.Revived)
                GameLogger.Log("SAVE", $"Mod '{modId}' is back — restoring its dormant data");

            // ─── Phase 2: fresh state, then core state ───
            // Every state owner resets first, so a mod without a slot in this
            // save starts from new-game defaults instead of the previous session.
            GameData.Instance.ResetRuntime("load");
            Bootstrap.AppServices.Current.ResetForNewGame();
            RuntimeSerializer.RestoreCoreState(coreSlot, GameData.Instance.Runtime);

            // ─── Phase 3: mod slots; a rejected slot is kept dormant, not lost ───
            var failed = new List<string>();
            var modErrors = ModSaveableRegistry.DispatchLoad(dormancy.Live, failed);
            foreach (var error in modErrors)
                GameLogger.Log("SAVE", $"Mod load error (data kept dormant): {error}", LogLevel.Error);
            foreach (var modId in failed)
            {
                dormancy.Dormant[modId] = SaveDormancy.MarkDormant(dormancy.Live[modId], SaveDormancy.Reason.LoadFailed, DateTime.UtcNow);
                dormancy.Live.Remove(modId);
                dormancy.NewlyDormant.Add((modId, SaveDormancy.Reason.LoadFailed));
            }

            ModSaveableRegistry.NotifyJoined(dormancy.Live.Keys);

            _dormant = dormancy.Dormant;
            LastLoadDormancy = dormancy;
            LastLoadedSceneName = RuntimeSerializer.ReadSlotPreview(coreSlot.Metadata).CurrentSceneName;
            return true;
        }

        /// <summary>
        /// Slots of the active game for the classic fixed grid: existing saves
        /// <c>slot_1..slot_N</c> plus empty placeholders. Kept for callers that
        /// still think in a fixed count; the save browser uses
        /// <see cref="ListSaves"/> with unlimited slots.
        /// </summary>
        public static List<SaveSlotInfo> GetSaveSlots(int slotCount)
        {
            var existing = ListSaves(Games.ActiveGame.Id).ToDictionary(s => s.FileName);
            var result = new List<SaveSlotInfo>();
            for (var i = 0; i < slotCount; i++)
            {
                var slotName = $"slot_{i + 1}";
                result.Add(existing.TryGetValue(slotName, out var info) ? info : EmptySlot(Games.ActiveGame.Id, slotName));
            }

            return result;
        }

        /// <summary>Godot directory of a game's saves; null/empty id is the shared root (saves made without a game).</summary>
        public static string GameDirectory(string gameId)
            => string.IsNullOrEmpty(gameId) ? SavesRoot : $"{SavesRoot}/{Sanitize(gameId)}";

        /// <summary>
        /// Every save of one game (null = saves made without a game), ordered by
        /// slot number. Slots are unlimited: the browser appends an empty slot
        /// named by <see cref="NextSlotName"/>.
        /// </summary>
        public static List<SaveSlotInfo> ListSaves(string gameId)
        {
            var result = new List<SaveSlotInfo>();
            var dir = GameDirectory(gameId);
            var absDir = GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(dir) : dir;
            if (!Directory.Exists(absDir))
                return result;

            foreach (var path in Directory.EnumerateFiles(absDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                var slotName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (slotName.StartsWith(HiddenSlotPrefix, StringComparison.Ordinal))
                    continue;
                var info = ReadSlotInfo(dir, slotName, path);
                if (info != null)
                    result.Add(info);
            }

            result.Sort((a, b) =>
            {
                var byNumber = SlotNumber(a.FileName).CompareTo(SlotNumber(b.FileName));
                return byNumber != 0 ? byNumber : string.CompareOrdinal(a.FileName, b.FileName);
            });
            return result;
        }

        /// <summary>
        /// Games that have saves on disk (newest first), plus the shared root
        /// as an entry with a null id when saves without a game exist.
        /// </summary>
        public static List<SaveGameSummary> ListGamesWithSaves()
        {
            var result = new List<SaveGameSummary>();
            var absRoot = GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(SavesRoot) : SavesRoot;
            if (!Directory.Exists(absRoot))
                return result;

            foreach (var gameDir in Directory.EnumerateDirectories(absRoot))
            {
                var gameId = System.IO.Path.GetFileName(gameDir);
                if (gameId == BackupDirectoryName)
                    continue;
                var saves = ListSaves(gameId);
                if (saves.Count > 0)
                    result.Add(Summarize(gameId, saves));
            }

            var legacy = ListSaves(null);
            if (legacy.Count > 0)
                result.Add(Summarize(null, legacy));

            result.Sort((a, b) => b.LastSave.CompareTo(a.LastSave));
            return result;
        }

        /// <summary>First free <c>slot_N</c> after the highest used number of the game.</summary>
        public static string NextSlotName(string gameId)
        {
            var max = 0;
            foreach (var save in ListSaves(gameId))
                max = Math.Max(max, SlotNumber(save.FileName) == int.MaxValue ? 0 : SlotNumber(save.FileName));
            return $"slot_{max + 1}";
        }

        /// <summary>Reads a save of any game without loading it (composition preview, game id).</summary>
        public static SavePackage ReadPackage(string gameId, string fileName)
        {
            var path = ProjectSettings.GlobalizePath($"{GameDirectory(gameId)}/{Sanitize(fileName)}.json");
            try
            {
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<SavePackage>(File.ReadAllText(path), JsonOptionsProvider.ReadOptions)
                    : null;
            }
            catch (Exception ex)
            {
                GameLogger.Log("SAVE", $"Could not read save '{path}': {ex.Message}", LogLevel.Warning);
                return null;
            }
        }

        /// <summary>
        /// Copies a save of <paramref name="gameId"/> into <c>backup/</c> next to
        /// it before it is loaded into a changed composition; returns the
        /// backup path.
        /// </summary>
        public static string BackupSave(string gameId, string fileName)
        {
            var gameDir = ProjectSettings.GlobalizePath(GameDirectory(gameId));
            var source = System.IO.Path.Combine(gameDir, Sanitize(fileName) + ".json");
            if (!File.Exists(source))
                return null;

            var backupDir = System.IO.Path.Combine(gameDir, BackupDirectoryName);
            Directory.CreateDirectory(backupDir);
            var target = System.IO.Path.Combine(backupDir, $"{Sanitize(fileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.Copy(source, target, overwrite: true);
            GameLogger.Log("SAVE", $"Backup of '{fileName}' before a composition change: {target}");
            return target;
        }

        /// <summary>
        /// Moves a save made without a game (shared root) into
        /// <paramref name="gameId"/>'s folder under a free slot name, together
        /// with its screenshot. Returns the new slot name.
        /// </summary>
        public static string AdoptLegacySave(string fileName, string gameId)
        {
            var slot = NextSlotName(gameId);
            var fromDir = ProjectSettings.GlobalizePath(GameDirectory(null));
            var toDir = ProjectSettings.GlobalizePath(GameDirectory(gameId));
            Directory.CreateDirectory(toDir);
            foreach (var extension in new[] { ".json", ".png" })
            {
                var from = System.IO.Path.Combine(fromDir, Sanitize(fileName) + extension);
                if (File.Exists(from))
                    File.Move(from, System.IO.Path.Combine(toDir, slot + extension));
            }

            GameLogger.Log("SAVE", $"Save '{fileName}' without a game moved to game '{gameId}' as '{slot}'");
            return slot;
        }

        /// <summary>
        /// Copies every save of <paramref name="fromGameId"/> into
        /// <paramref name="toGameId"/> (a duplicated game), rewriting the game id
        /// inside each file so the copy owns them. Returns the number copied.
        /// </summary>
        public static int CopyGameSaves(string fromGameId, string toGameId)
        {
            var fromDir = ProjectSettings.GlobalizePath(GameDirectory(fromGameId));
            var toDir = ProjectSettings.GlobalizePath(GameDirectory(toGameId));
            if (!Directory.Exists(fromDir) || string.IsNullOrEmpty(toGameId))
                return 0;

            Directory.CreateDirectory(toDir);
            var copied = 0;
            foreach (var path in Directory.EnumerateFiles(fromDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var pkg = JsonSerializer.Deserialize<SavePackage>(File.ReadAllText(path), JsonOptionsProvider.ReadOptions);
                    if (pkg == null)
                        continue;
                    pkg.Game ??= new SaveGameInfo();
                    pkg.Game.Id = toGameId;
                    var name = System.IO.Path.GetFileName(path);
                    File.WriteAllText(System.IO.Path.Combine(toDir, name), JsonSerializer.Serialize(pkg, JsonOptionsProvider.WriteCompactOptions));
                    var screenshot = System.IO.Path.ChangeExtension(path, ".png");
                    if (File.Exists(screenshot))
                        File.Copy(screenshot, System.IO.Path.Combine(toDir, System.IO.Path.GetFileName(screenshot)), overwrite: true);
                    copied++;
                }
                catch (Exception ex)
                {
                    GameLogger.Log("SAVE", $"Could not copy save '{path}': {ex.Message}", LogLevel.Warning);
                }
            }

            GameLogger.Log("SAVE", $"Copied {copied} save(s) from game '{fromGameId}' to '{toGameId}'");
            return copied;
        }

        private const string BackupDirectoryName = "backup";

        private static SaveGameSummary Summarize(string gameId, List<SaveSlotInfo> saves)
        {
            var newest = saves.OrderByDescending(s => s.SaveTime).First();
            return new SaveGameSummary
            {
                GameId = gameId,
                GameName = newest.GameName ?? gameId,
                SaveCount = saves.Count,
                LastSave = newest.SaveTime,
                LastFileName = newest.FileName,
            };
        }

        private static SaveSlotInfo ReadSlotInfo(string godotDir, string slotName, string absolutePath)
        {
            try
            {
                var pkg = JsonSerializer.Deserialize<SavePackage>(File.ReadAllText(absolutePath), JsonOptionsProvider.ReadOptions);
                var preview = new RuntimeSerializer.CoreSlotPreview();
                if (pkg?.ModData != null && pkg.ModData.TryGetValue(SavePackage.CoreSlotKey, out var coreSlot))
                    preview = RuntimeSerializer.ReadSlotPreview(coreSlot.Metadata);

                var screenshot = $"{godotDir}/{slotName}.png";
                return new SaveSlotInfo
                {
                    FileName = slotName,
                    SaveTime = pkg?.Timestamp ?? DateTime.MinValue,
                    SceneName = preview.CurrentSceneName ?? "unknown",
                    HasScreenshot = File.Exists(System.IO.Path.ChangeExtension(absolutePath, ".png")),
                    IsEmpty = false,
                    ScreenshotPath = screenshot,
                    GameTime = preview.GameTime,
                    CharacterCount = preview.CharacterCount,
                    LampPercent = 0f,
                    GameId = pkg?.Game?.Id,
                    GameName = pkg?.Game?.Name,
                    Mods = pkg?.Game?.Mods,
                };
            }
            catch (Exception ex)
            {
                GameLogger.Log("SAVE", $"Skipping unreadable save '{absolutePath}': {ex.Message}", LogLevel.Warning);
                return null;
            }
        }

        /// <summary>Empty placeholder for a free slot of a game.</summary>
        public static SaveSlotInfo EmptySlot(string gameId, string slotName) => new()
        {
            FileName = slotName,
            SaveTime = DateTime.MinValue,
            SceneName = "",
            HasScreenshot = false,
            IsEmpty = true,
            ScreenshotPath = $"{GameDirectory(gameId)}/{slotName}.png",
            GameId = gameId,
        };

        private static int SlotNumber(string fileName)
            => fileName != null && fileName.StartsWith("slot_", StringComparison.Ordinal)
               && int.TryParse(fileName.Substring(5), out var number)
                ? number
                : int.MaxValue;

        /// <summary>
        /// Removes dormant slots from every save of every game (and from the
        /// running session), returning how many slots were dropped. Offered in
        /// the save settings: dormant data is kept indefinitely otherwise.
        /// </summary>
        public static int PurgeDormantData()
        {
            var purged = _dormant.Count;
            ClearDormant();

            var root = GameEnvironment.IsRuntime ? ProjectSettings.GlobalizePath(SavesRoot) : "saves";
            if (!Directory.Exists(root))
                return purged;

            foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var pkg = JsonSerializer.Deserialize<SavePackage>(File.ReadAllText(path), JsonOptionsProvider.ReadOptions);
                    if (pkg?.Dormant == null || pkg.Dormant.Count == 0)
                        continue;

                    purged += pkg.Dormant.Count;
                    pkg.Dormant = null;
                    File.WriteAllText(path, JsonSerializer.Serialize(pkg, JsonOptionsProvider.WriteCompactOptions));
                }
                catch (Exception ex)
                {
                    GameLogger.Log("SAVE", $"Could not purge dormant data in '{path}': {ex.Message}", LogLevel.Warning);
                }
            }

            GameLogger.Log("SAVE", $"Purged {purged} dormant slot(s)");
            return purged;
        }

        /// <summary>Snapshot of the active game for <see cref="SavePackage.Game"/>; null without an active game.</summary>
        private static SaveGameInfo BuildGameInfo()
        {
            if (!Games.ActiveGame.IsActive)
                return null;

            var info = new SaveGameInfo
            {
                Id = Games.ActiveGame.Id,
                Name = Games.ActiveGame.Collection?.DisplayTitle,
                Version = Games.ActiveGame.Collection?.Version,
            };
            foreach (var modId in ModRegistry.Instance.GetOrderedModIds())
                info.Mods.Add(new SaveGameMod { Id = modId, Version = ModManager.Instance.GetMod(modId)?.Version });
            return info;
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
