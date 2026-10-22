namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Events;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Quests;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Dialog.Executable;
    using Godot;

    /// <summary>
    /// The studio's connection to the running game, parked at <c>/root</c> for
    /// the whole session (the dock comes and goes, this stays). It installs
    /// the <see cref="StudioDebugger"/> into the dialog runtime, keeps the
    /// <see cref="StudioJournal"/> fed from the event bus, owns the edit
    /// <see cref="StudioLayer"/>, the per-scenario <see cref="StudioWorkingCopy"/>s
    /// and the rollback <see cref="StudioSnapshot"/> of the running scenario, and
    /// performs every action of the dock: apply edits, the restart modes, run a
    /// scene candidate, go to a scene, create scenes and scenarios, re-read
    /// backgrounds dropped in while the game runs, change state values, export
    /// the layer.
    /// <para/>
    /// <see cref="Version"/> grows on every change the dock may want to show;
    /// the dock polls it instead of subscribing, so a burst of dialog actions
    /// costs one redraw.
    /// </summary>
    public partial class StudioLive : Node
    {
        private const string ChainGroup = "main_event_chain";
        private static readonly Regex ModIdPattern = new("^[a-z][a-z0-9_]*$");

        private readonly Dictionary<string, StudioWorkingCopy> _copies = new();
        private StudioSnapshot _snapshot;
        private StudioSnapshot _checkpoint;

        public static StudioLive Instance { get; private set; }

        public StudioDebugger Debugger { get; } = new();
        public StudioJournal Journal { get; } = new();
        public StudioLayer Layer { get; }

        /// <summary>Grows on every change worth redrawing.</summary>
        public int Version { get; private set; }

        public StudioLive(StudioLayer layer)
        {
            Name = "ScenarioStudioLive";
            Layer = layer;
        }

        public override void _EnterTree()
        {
            Instance = this;
            DialogRuntime.Debugger = Debugger;
            Debugger.ScenarioStarted += OnScenarioStarted;
            Debugger.Held += OnHeld;
            Debugger.Changed += Touch;
            ModEventBus.Subscribe<SceneEnteredEvent>(OnSceneEntered, priority: 50);
            ModEventBus.Subscribe<SceneComposedEvent>(OnSceneComposed);
            ModEventBus.Subscribe<DialogEndedEvent>(OnDialogEnded);
        }

        public override void _ExitTree()
        {
            if (DialogRuntime.Debugger == Debugger)
                DialogRuntime.Debugger = null;
            ModEventBus.Unsubscribe<SceneEnteredEvent>(OnSceneEntered);
            ModEventBus.Unsubscribe<SceneComposedEvent>(OnSceneComposed);
            ModEventBus.Unsubscribe<DialogEndedEvent>(OnDialogEnded);
            if (Instance == this)
                Instance = null;
        }

        // ── what is running ────────────────────────────────────────

        public DialogBox Box => SceneContextNode.FindNode<DialogBox>("DialogBox");

        public static string SceneName => SceneManager.GetCurrentSceneName();

        /// <summary>The scenario in the dialog box, or the last one the debugger saw while it still exists.</summary>
        public ScenarioEvent CurrentEvent
        {
            get
            {
                var box = Box;
                if (box != null && box.IsRunning && box.Locker is ScenarioEvent running)
                    return running;
                var last = Debugger.Event;
                return last != null && IsInstanceValid(last) ? last : null;
            }
        }

        /// <summary>True while <see cref="CurrentEvent"/> is the dialog in the box.</summary>
        public bool IsCurrentRunning
        {
            get
            {
                var box = Box;
                return box != null && box.IsRunning && box.Locker == CurrentEvent;
            }
        }

        /// <summary>Scenario line of the action the running dialog stands on; 0 when nothing runs.</summary>
        public int CurrentLine()
        {
            if (!IsCurrentRunning)
                return 0;

            var runtime = Box.Runtime;
            var queue = runtime.Dialog?.Queue;
            var index = runtime.Index;
            return queue != null && index >= 0 && index < queue.Count ? queue[index]?.SourceLine ?? 0 : 0;
        }

        /// <summary>True when the running dialog has an action built from scenario line <paramref name="line"/>, so it can run next.</summary>
        public bool CanRunFrom(int line)
        {
            var queue = IsCurrentRunning ? Box.Runtime.Dialog?.Queue : null;
            return line > 0 && queue != null && StudioDebugger.IndexOf(queue, line) >= 0;
        }

        /// <summary>Makes scenario line <paramref name="line"/> the next one to run (<see cref="StudioDebugger.SetNext"/>) and logs it.</summary>
        public bool SetNextLine(int line)
        {
            if (!Debugger.SetNext(Box, line))
                return false;
            Journal.Add(StudioLogCategory.Stop, new StudioText("studio/log_text_next_line", line));
            return true;
        }

        /// <summary>Current text of a scenario event's file (edits applied so far), or its built-in text.</summary>
        public static string SourceText(ScenarioEvent evt)
        {
            if (evt == null)
                return null;
            if (!string.IsNullOrEmpty(evt.SourceModId))
                return ScenarioDefs.ReadText(evt.SourceModId, evt.SourcePath) ?? evt.ScenarioText;
            return evt.ScenarioText;
        }

        /// <summary>The mod (or layer) a scenario file belongs to and how the studio may touch it.</summary>
        public StudioModScope ScopeOf(string modId, string path)
        {
            if (string.IsNullOrEmpty(modId))
                return new StudioModScope("inline", StudioScopeKind.ReadOnly);
            if (modId == StudioLayer.Id)
                return new StudioModScope(modId, StudioScopeKind.Layer);
            return string.IsNullOrEmpty(ModManager.Instance.GetFileSystemPath(modId, path))
                ? new StudioModScope(modId, StudioScopeKind.Overlay)
                : new StudioModScope(modId, StudioScopeKind.Own);
        }

        /// <summary>Where an edit of a file in <paramref name="fileScope"/> is written.</summary>
        public StudioModScope EditScopeFor(StudioModScope fileScope)
        {
            if (fileScope.Kind == StudioScopeKind.Own && !Layer.ForceLayer)
                return fileScope;
            return new StudioModScope(StudioLayer.Id, StudioScopeKind.Layer);
        }

        // ── edits ──────────────────────────────────────────────────

        /// <summary>Pending edits of a scenario, or null.</summary>
        public StudioWorkingCopy Copy(string scenarioName) =>
            scenarioName != null && _copies.TryGetValue(scenarioName, out var copy) ? copy : null;

        /// <summary>Pending edits of the current scenario, created from its file on first use; null without a scenario.</summary>
        public StudioWorkingCopy EnsureCopy()
        {
            var evt = CurrentEvent;
            if (evt == null)
                return null;
            if (!_copies.TryGetValue(evt.ID, out var copy))
                _copies[evt.ID] = copy = new StudioWorkingCopy(evt.ID, SourceText(evt));
            return copy;
        }

        /// <summary>Inserts lines before 0-based line <paramref name="index"/> of the current scenario.</summary>
        public void Insert(int index, params string[] lines)
        {
            EnsureCopy()?.Insert(index, lines);
            Touch();
        }

        public void ReplaceLine(int index, string line)
        {
            EnsureCopy()?.Replace(index, line);
            Touch();
        }

        public void ReplaceText(string text)
        {
            EnsureCopy()?.ReplaceAll(text);
            Touch();
        }

        /// <summary>Points the answer at 0-based line <paramref name="index"/> to a scene (a label block leading there is added).</summary>
        public void TargetScene(int index, string scene)
        {
            var copy = EnsureCopy();
            if (copy == null)
                return;
            var label = StudioScriptEdits.EnsureSceneExit(copy, scene);
            copy.Replace(index, StudioScriptEdits.WithTarget(copy.Line(index), label));
            Touch();
        }

        /// <summary>Points the answer at 0-based line <paramref name="index"/> to a label of the scenario.</summary>
        public void TargetLabel(int index, string label)
        {
            var copy = EnsureCopy();
            if (copy == null)
                return;
            copy.Replace(index, StudioScriptEdits.WithTarget(copy.Line(index), label));
            Touch();
        }

        /// <summary>Points the answer at 0-based line <paramref name="index"/> to the end of the scenario.</summary>
        public void TargetEnd(int index)
        {
            var copy = EnsureCopy();
            if (copy == null)
                return;
            var label = StudioScriptEdits.EnsureEnd(copy);
            copy.Replace(index, StudioScriptEdits.WithTarget(copy.Line(index), label));
            Touch();
        }

        public void Discard()
        {
            var evt = CurrentEvent;
            if (evt != null && _copies.Remove(evt.ID))
                Touch();
        }

        /// <summary>
        /// Writes the pending edits of the current scenario — into its folder mod,
        /// or into the studio layer for zip mods, layer files and forced layer —
        /// and replays the scenario from its start with the game state rolled
        /// back. Returns the scope written to, or null when there was nothing.
        /// </summary>
        public StudioModScope Apply()
        {
            var evt = CurrentEvent;
            var copy = Copy(evt?.ID);
            if (copy == null || copy.PendingCount == 0)
                return null;

            if (string.IsNullOrEmpty(evt.SourceModId))
            {
                evt.ScenarioText = copy.Text;
                _copies.Remove(evt.ID);
                Restart(StudioRestartMode.Scenario);
                return ScopeOf(null, null);
            }

            var target = EditScopeFor(ScopeOf(evt.SourceModId, evt.SourcePath));
            var pending = copy.PendingCount;
            if (target.Kind == StudioScopeKind.Layer)
                Layer.Write(evt.SourcePath, copy.Text);
            else
                File.WriteAllText(ModManager.Instance.GetFileSystemPath(evt.SourceModId, evt.SourcePath), copy.Text);

            _copies.Remove(evt.ID);
            SceneManager.ReloadContent();
            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_applied", evt.ID, pending, target.ModId));
            GameLogger.Log("UI", $"Scenario studio: applied {pending} change(s) of '{evt.ID}' to '{target.ModId}'");
            Restart(StudioRestartMode.Rollback);
            return target;
        }

        // ── restarts and navigation ────────────────────────────────

        public void Restart(StudioRestartMode mode)
        {
            var evt = CurrentEvent;
            if (evt == null)
                return;

            var name = evt.ID;
            switch (mode)
            {
                case StudioRestartMode.Scene:
                    GoToScene(SceneName);
                    return;
                case StudioRestartMode.Rollback when _snapshot != null && _snapshot.ScenarioName == name:
                    StopDialog();
                    if (_snapshot.Restore())
                        ForgetCompleted(name);
                    RunScenario(name, null, evt);
                    return;
                case StudioRestartMode.FromLabel:
                    var label = LabelAbove(SourceText(evt), CurrentLine());
                    StopDialog();
                    ForgetCompleted(name);
                    RunScenario(name, label, evt);
                    return;
                default:
                    StopDialog();
                    ForgetCompleted(name);
                    RunScenario(name, null, evt);
                    return;
            }
        }

        /// <summary>Starts scenario <paramref name="name"/> of the current scene now, stopping the running dialog.</summary>
        public bool RunNow(string name)
        {
            StopDialog();
            ForgetCompleted(name);
            return RunScenario(name, null, null);
        }

        /// <summary>Removes <paramref name="name"/> from the one-run completion history.</summary>
        public void ForgetCompleted(string name)
        {
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null)
                return;
            runtime.CompletedScenarioIds.Remove(name);
            runtime.LoadedCompletedScenarioIds?.Remove(name);
            Touch();
        }

        /// <summary>Leaves the running dialog and enters <paramref name="scene"/> (a debugging teleport).</summary>
        public void GoToScene(string scene)
        {
            if (string.IsNullOrEmpty(scene))
                return;
            StopDialog();
            Debugger.Reset();
            GetNodeOrNull<SceneManager>("/root/SceneManager")?.SwitchScene(scene);
            Touch();
        }

        private void StopDialog()
        {
            Debugger.Continue(null);
            Box?.Abort();
        }

        /// <summary>
        /// Builds a fresh event for scenario <paramref name="name"/> from the
        /// current definitions (edits included), puts it on the scene's chain in
        /// place of <paramref name="replaced"/> and starts it at once, so other
        /// queued scenarios do not run first. A scenario without a definition
        /// (test play, inline) is rebuilt from the replaced event's text.
        /// </summary>
        private bool RunScenario(string name, string startLabel, ScenarioEvent replaced)
        {
            var chain = GetTree().GetFirstNodeInGroup(ChainGroup) as ExecutableMainEventChain;
            var factory = AppServices.Current.SceneEvents.ScenarioEventFactory;
            if (chain == null || factory == null)
            {
                GameLogger.Log("UI", $"Scenario studio: cannot run '{name}' — no event chain or scenario factory", LogLevel.Error);
                return false;
            }

            var def = FindDefinition(name);
            ScenarioEvent evt;
            if (def != null)
            {
                var text = ScenarioDefs.ReadText(def.ModId, def.FilePath);
                evt = factory.CreateFromDefinition(text, name, def.Condition, def.LightUseTime, def.IsOneRun, def.ModId, def.FilePath) as ScenarioEvent;
            }
            else if (replaced != null && IsInstanceValid(replaced))
            {
                evt = factory.CreateFromText(replaced.ScenarioText) as ScenarioEvent;
            }
            else
            {
                GameLogger.Log("UI", $"Scenario studio: scenario '{name}' not found", LogLevel.Error);
                return false;
            }

            if (evt == null)
                return false;

            evt.StartLabel = startLabel;
            if (replaced != null && IsInstanceValid(replaced) && replaced.GetParent() == chain)
                chain.RemoveEvent(replaced);
            chain.AddEvent(evt);

            var started = evt.RunDialog();
            GameLogger.Log("UI", $"Scenario studio: run '{name}'" + (startLabel != null ? $" from :{startLabel}" : "") + (started ? "" : " — dialog box busy"));
            Touch();
            return started;
        }

        public static ScenarioDefinition FindDefinition(string name)
        {
            var defs = ScenarioDefs.CollectDefinitions();
            if (defs == null)
                return null;
            foreach (var list in defs.Values)
            {
                foreach (var def in list)
                {
                    if (def.Name == name)
                        return def;
                }
            }
            return null;
        }

        private static string LabelAbove(string text, int line)
        {
            if (text == null || line <= 0)
                return null;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (var i = Math.Min(line, lines.Length) - 1; i >= 0; i--)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith(':'))
                    return trimmed.Substring(1).Trim();
            }
            return null;
        }

        /// <summary>True when a manual checkpoint was saved in this session.</summary>
        public bool HasCheckpoint => _checkpoint != null;

        /// <summary>Remembers the whole game state and the scene now, to come back to with <see cref="RestoreCheckpoint"/>.</summary>
        public void SaveCheckpoint()
        {
            _checkpoint = StudioSnapshot.Capture(null, SceneName);
            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_checkpoint_saved", SceneName));
            Touch();
        }

        /// <summary>Brings back the saved checkpoint: game state, then its scene entered again.</summary>
        public bool RestoreCheckpoint()
        {
            if (_checkpoint == null)
                return false;

            StopDialog();
            Debugger.Reset();
            if (!_checkpoint.Restore())
                return false;
            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_checkpoint_restored", _checkpoint.SceneName));
            GoToScene(_checkpoint.SceneName);
            return true;
        }

        // ── new content ────────────────────────────────────────────

        /// <summary>The scene or mod id typed by the author, trimmed and lowercased; null when it is not one (latin letters, digits, _).</summary>
        public static string ValidName(string name)
        {
            name = (name ?? string.Empty).Trim().ToLowerInvariant();
            return ModIdPattern.IsMatch(name) ? name : null;
        }

        /// <summary>
        /// Creates a scene in the layer with a first scenario; returns false on a bad name.
        /// The scene shows <paramref name="background"/>, or the backgrounds of the current scene when it is null.
        /// </summary>
        public bool CreateScene(string name, string background = null)
        {
            name = ValidName(name);
            if (name == null)
                return false;

            List<string> backgrounds = null;
            if (background != null)
            {
                backgrounds = new List<string> { background };
            }
            else
            {
                var current = SceneName;
                var defs = AppServices.Current.SceneDefs.CollectAll();
                if (current != null && defs != null && defs.TryGetValue(current, out var list) && list.Count > 0)
                    backgrounds = list[0].DefaultBackground;
            }

            var scenario = Layer.CreateScene(name, backgrounds, StudioScriptEdits.NewLine());
            SceneManager.ReloadContent();
            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_scene_created", name, scenario));
            Touch();
            return true;
        }

        /// <summary>Creates a new scenario for the current scene in the layer; returns its name.</summary>
        public string CreateScenario()
        {
            var scene = SceneName;
            if (string.IsNullOrEmpty(scene))
                return null;
            var name = Layer.CreateScenario(scene, StudioScriptEdits.NewLine());
            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_scenario_created", name, scene));
            Touch();
            return name;
        }

        /// <summary>
        /// Re-reads the backgrounds after the author dropped pictures into a mod or
        /// the layer (<see cref="StudioBackgrounds.Reload"/>): the current background
        /// is shown from its file again, new ones can be picked for a scene.
        /// </summary>
        public StudioAssetReload ReloadAssets()
        {
            var before = new HashSet<string>(StudioBackgrounds.Ids());
            StudioBackgrounds.Reload();

            var result = new StudioAssetReload();
            foreach (var id in StudioBackgrounds.Ids())
            {
                result.Total++;
                if (!before.Contains(id))
                    result.Added.Add(id);
            }

            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_assets_reloaded", result.Total, result.Added.Count));
            GameLogger.Log("UI", $"Scenario studio: backgrounds re-read, {result.Total} in total, new: [{string.Join(", ", result.Added)}]");
            Touch();
            return result;
        }

        /// <summary>Exports the layer as mod <paramref name="modId"/>; null on a bad id.</summary>
        public string Export(string modId)
        {
            modId = ValidName(modId);
            if (modId == null)
                return null;
            var path = Layer.Export(modId);
            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_exported", modId));
            Touch();
            return path;
        }

        // ── state ──────────────────────────────────────────────────

        /// <summary>Writes a value edited in the "State" tab back into the game.</summary>
        public void EditState(StudioStateEntry entry, string value)
        {
            switch (entry.Editor)
            {
                case StudioStateEditor.DialogVariable:
                    Box?.Runtime.SetVariable(entry.Id, value);
                    break;
                case StudioStateEditor.Inventory:
                    var inventory = GameData.Instance?.Runtime.Inventory;
                    if (inventory == null || !int.TryParse(value, out var count) || count < 0)
                        return;
                    var delta = count - inventory.CheckCount(entry.Id);
                    if (delta > 0)
                        inventory.Add(entry.Id, delta);
                    else if (delta < 0)
                        inventory.Remove(entry.Id, -delta);
                    break;
                case StudioStateEditor.Quest:
                    QuestServiceRegistry.Provider?.SetStatus(entry.Id, value);
                    break;
                default:
                    return;
            }

            Journal.Add(StudioLogCategory.Edit, new StudioText("studio/log_text_state", entry.Id, value));
            Touch();
        }

        // ── bus and debugger callbacks ─────────────────────────────

        private void OnScenarioStarted(ScenarioEvent evt)
        {
            try
            {
                _snapshot = StudioSnapshot.Capture(evt.ID, SceneName);
            }
            catch (Exception ex)
            {
                _snapshot = null;
                GameLogger.Log("UI", $"Scenario studio: snapshot of '{evt.ID}' failed: {ex.Message}", LogLevel.Warning);
            }
            Journal.Add(StudioLogCategory.Scenario, new StudioText("studio/log_text_started", evt.ID));
        }

        private void OnHeld(int line, bool breakpoint)
        {
            Journal.Add(StudioLogCategory.Stop, new StudioText(breakpoint ? "studio/log_text_breakpoint" : "studio/log_text_paused", line));
        }

        private void OnSceneEntered(SceneEnteredEvent e)
        {
            Journal.OnSceneEntered(e);
            Touch();
        }

        private void OnSceneComposed(SceneComposedEvent e)
        {
            Journal.OnSceneComposed(e);
            Touch();
        }

        private void OnDialogEnded(DialogEndedEvent e)
        {
            Journal.OnDialogEnded(e);
            Touch();
        }

        private void Touch() => Version++;
    }
}
