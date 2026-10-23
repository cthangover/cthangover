namespace Cthangover.Mods.ScenarioStudio.Tests.Live
{
    using System.IO;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.UI;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Dialog.Action.Impls;
    using Cthangover.Mods.Dialog.Executable;
    using Cthangover.Mods.Interface.View;
    using Godot;

    /// <summary>
    /// Drives the studio on the real kitchen scenario the way a player would,
    /// one step per tick, checking after each step what it must have changed:
    /// <list type="number">
    /// <item>waits for the kitchen scenario's question, opens the dock with the
    ///   <c>studio</c> UI toggle (the toolbar button) and checks the current
    ///   line is the running action's line;</item>
    /// <item>widens the dock with its left grip and moves it by the header;</item>
    /// <item>pause and continue;</item>
    /// <item>novice mode: the question and its answers with outcomes; adds an
    ///   answer (broken), redirects it to a scene through "Choose where",
    ///   inserts a line before the question;</item>
    /// <item>applies: the scenario restarts with rollback and first shows the
    ///   inserted line, the edit is in the test layer and the cooking mod's
    ///   file is untouched; the question then has the new answer;</item>
    /// <item>breakpoint on the question, restart, hold before it; drags the
    ///   ▶ marker back onto the inserted line, continues from there and is held
    ///   by the breakpoint again, continue;</item>
    /// <item>the Scene, State (a checkpoint is saved there), Log and Map tabs;</item>
    /// <item>drops a picture into the layer's <c>backgrounds/</c> and reloads
    ///   the resources: it becomes a layer background;</item>
    /// <item>creates a scene in the layer on that background, goes there and sees
    ///   its scenario run from the layer and the picture on screen; replaces the
    ///   picture, reloads, and the new file is shown; exports the layer as a mod
    ///   (background included), returns to the checkpoint and closes the dock.</item>
    /// </list>
    /// The test layer is deleted at the end. Ends with
    /// <c>[TEST_RESULT] name=studio_live passed=N failed=M</c>.
    /// </summary>
    public partial class StudioLiveDriverNode : Node
    {
        private const string ResultName = "studio_live";
        private const string EntryId = "studio";
        private const string Root = "Frame/Dock/Column/";
        private const float TickSeconds = 0.5f;
        private static readonly Vector2 Widen = new(-100, 0);
        private static readonly Vector2 Shift = new(-60, -40);
        private const float TimeoutSeconds = 150f;
        private const string NewScene = "studio_test_yard";
        private const string ExportId = "studio_test_export";
        private const string LayerBackground = "studio_test_backdrop";
        private static readonly Vector2I FirstSize = new(64, 32);
        private static readonly Vector2I SecondSize = new(48, 48);

        private readonly string _shotsDir;
        private StudioPanel _panel;
        private string _scenario;
        private string _sourceMod;
        private string _sourcePath;
        private string _originalText;
        private int _questionLine;
        private int _answers;
        private int _movedLine;
        private Rect2 _frameRect;
        private int _step = -1;
        private int _passed;
        private int _failed;
        private double _elapsed;
        private bool _finished;

        public StudioLiveDriverNode(string shotsDir)
        {
            _shotsDir = shotsDir;
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private static StudioLive Live => StudioLive.Instance;

        private DialogBox Box => Live?.Box;

        private void Tick()
        {
            if (_finished)
                return;

            _elapsed += TickSeconds;
            if (_elapsed >= TimeoutSeconds)
            {
                Fail($"timeout ({TimeoutSeconds:0}s) at step {_step}");
                Finish();
                return;
            }

            if (_step < 0 && !QuestionIsUp())
                return;

            _step++;
            switch (_step)
            {
                case 0:
                    var scenario = (ScenarioEvent)Box.Locker;
                    _scenario = scenario.ID;
                    _sourceMod = scenario.SourceModId;
                    _sourcePath = scenario.SourcePath;
                    _originalText = ModManager.Instance.ReadFileText(_sourceMod, _sourcePath);
                    _questionLine = CurrentAction()?.SourceLine ?? 0;
                    _answers = Box.AnswerVariantCount;
                    Check(Live != null && Live.Layer.ForceLayer, "studio live is up with the layer forced");
                    Check(_sourceMod != null && _sourcePath != null, $"running scenario '{_scenario}' knows its file ({_sourceMod}:{_sourcePath})");
                    Check(_questionLine > 0, $"question action carries its source line ({_questionLine})");
                    AppServices.Current.Ui.ToggleEntry(EntryId);
                    break;
                case 1:
                    Check(UiHost.Instance.TryGetInstance(EntryId, out var control) && control is StudioPanel, "dock opened by the toggle");
                    _panel = control as StudioPanel;
                    if (_panel == null)
                    {
                        Finish();
                        return;
                    }
                    var session = _panel.Session;
                    Check(session.ScenarioName == _scenario, $"dock shows the running scenario ('{session.ScenarioName}')");
                    Check(IsCurrentLine(session, _questionLine), "current line is the question's line");
                    Check(session.FileScope?.ModId == _sourceMod, $"file scope is the scenario's mod ({session.FileScope?.ModId})");
                    Check(session.EditScope?.Kind == StudioScopeKind.Layer, "edits go to the studio layer");
                    Check(Mathf.Abs(_panel.GetNode<Control>("Frame/Dock").Size.X - StudioStyles.DockWidth) < 1, "dock keeps its width");
                    Shot("author");
                    _frameRect = FrameRect();
                    Drag("Frame/LeftGrip", Widen);
                    Press(Root + "Transport/Row/PauseButton");
                    break;
                case 2:
                    var widened = FrameRect();
                    Check(Mathf.Abs(widened.Size.X - (_frameRect.Size.X - Widen.X)) < 1 && Mathf.Abs(widened.End.X - _frameRect.End.X) < 1,
                        $"left grip widens the dock to the left ({_frameRect} → {widened})");
                    Check(Mathf.Abs(_panel.GetNode<Control>("Frame/Dock").Size.X - widened.Size.X) < 1, "the panel fills the widened frame");
                    _frameRect = widened;
                    Drag(Root + "Header", Shift);
                    Check(Live.Debugger.IsPaused && _panel.Session.RunState == StudioRunState.Paused, "pause is on");
                    Press(Root + "Transport/Row/ContinueButton");
                    Press(Root + "Header/Row/ModeSwitch/Items/NoviceButton");
                    break;
                case 3:
                    var moved = FrameRect();
                    Check(moved.Position.DistanceTo(_frameRect.Position + Shift) < 1 && moved.Size.DistanceTo(_frameRect.Size) < 1,
                        $"header drag moves the dock ({_frameRect} → {moved})");
                    Check(!Live.Debugger.IsPaused, "continue turns pause off");
                    Check(_panel.Mode == StudioMode.Novice, "novice mode selected");
                    Check(CountOf<StudioSayView>() == 1, "novice: the question on screen");
                    Check(CountOf<StudioBranchView>() == _answers, $"novice: {_answers} answers");
                    Check(Broken() == 0, "novice: every answer leads somewhere");
                    PressText("studio/add_option");
                    break;
                case 4:
                    Check(CountOf<StudioBranchView>() == _answers + 1, "new answer added");
                    Check(Broken() == 1, "the new answer leads nowhere yet");
                    PressBrokenTarget();
                    break;
                case 5:
                    PickScene("home_vestibule");
                    break;
                case 6:
                    Check(Broken() == 0, "the new answer now leads to a scene");
                    PressText("studio/add_line_before");
                    break;
                case 7:
                    Check(CountOf<StudioSayView>() == 2, "new line shown before the question");
                    Check(_panel.Session.PendingCount > 0, $"pending edits counted ({_panel.Session.PendingCount})");
                    Shot("novice");
                    Press(Root + "Footer/Row/ApplyButton");
                    break;
                case 8:
                    var line = CurrentAction() as ActionText;
                    Check(Box.IsRunning && Box.Locker is ScenarioEvent { ID: var id } && id == _scenario, "scenario restarted after apply");
                    Check(line != null && line.Text == TranslationServer.Translate("studio/new_line_text"), $"restart shows the inserted line first ('{line?.Text}')");
                    Check(File.Exists(Path.Combine(Live.Layer.RootPath, _sourcePath)), "edit written to the studio layer");
                    Check(ModManager.Instance.ReadFileText(_sourceMod, _sourcePath) == _originalText, "the mod's own file is untouched");
                    Shot("applied");
                    Box.NextAction();
                    break;
                case 9:
                    Check(Box.IsWaitingAnswer && Box.AnswerVariantCount == _answers + 1, $"question now has the new answer ({Box.AnswerVariantCount})");
                    _questionLine = CurrentAction()?.SourceLine ?? 0;
                    Press(Root + "Header/Row/ModeSwitch/Items/AuthorButton");
                    break;
                case 10:
                    ToggleBreakpoint(_questionLine);
                    break;
                case 11:
                    Check(Live.Debugger.HasBreakpoint(_scenario, _questionLine), $"breakpoint set on line {_questionLine}");
                    Restart(StudioRestartMode.Scenario);
                    break;
                case 12:
                    Box.NextAction();
                    break;
                case 13:
                    Check(Box.Runtime.IsHeld && Box.AnswerVariantCount == 0, "held at the breakpoint before the question runs");
                    Check(_panel.Session.RunState == StudioRunState.Held, "dock shows the hold");
                    Shot("breakpoint");
                    DragMarkerBack();
                    break;
                case 14:
                    Check(Box.Runtime.IsHeld && Live.CurrentLine() == _movedLine && Box.AnswerVariantCount == 0,
                        $"marker drag makes line {_movedLine} the next one (now {Live.CurrentLine()})");
                    Check(IsCurrentLine(_panel.Session, _movedLine), "dock shows the moved line as current");
                    Press(Root + "Transport/Row/ContinueButton");
                    break;
                case 15:
                    var rerun = CurrentAction() as ActionText;
                    Check(!Box.Runtime.IsHeld && rerun != null && rerun.Text == TranslationServer.Translate("studio/new_line_text"),
                        $"continue runs from the moved line ('{rerun?.Text}')");
                    Box.NextAction();
                    break;
                case 16:
                    Check(Box.Runtime.IsHeld && Live.CurrentLine() == _questionLine, "the breakpoint holds the question again");
                    Press(Root + "Transport/Row/ContinueButton");
                    break;
                case 17:
                    Check(!Box.Runtime.IsHeld && Box.AnswerVariantCount == _answers + 1, "continue runs the question");
                    Press(Root + "Tabs/Row/SceneTab");
                    break;
                case 18:
                    Check(_panel.Tab == StudioTab.Scene && IsRunningCandidate(_panel.Session, _scenario), "scene tab: the scenario is the running candidate");
                    Shot("scene");
                    Press(Root + "Tabs/Row/StateTab");
                    break;
                case 19:
                    Check(_panel.Session.State.Count > 0 && CountOf<StudioStateRow>() == _panel.Session.State.Count, "state tab: a row per state value");
                    Shot("state");
                    MenuPick(100);
                    Check(Live.HasCheckpoint, "checkpoint remembered from the restart menu");
                    Press(Root + "Tabs/Row/LogTab");
                    break;
                case 20:
                    Check(HasEditLog(_panel.Session), "log tab: the applied edit is logged");
                    Shot("log");
                    Press(Root + "Tabs/Row/MapTab");
                    break;
                case 21:
                    Check(HasNode(_panel.Session, StudioNodeState.Here, "home_kitchen"), "map tab: you are in the kitchen");
                    Check(HasNode(_panel.Session, StudioNodeState.Next, "home_vestibule"), "map tab: the vestibule is an exit");
                    Shot("map");
                    Check(!StudioBackgrounds.Ids().Contains(LayerBackground), "a picture not dropped yet is no background");
                    WriteLayerBackground(FirstSize);
                    Press(Root + "Transport/Row/ReloadButton");
                    break;
                case 22:
                    Check(StudioBackgrounds.SourceOf(LayerBackground) == StudioLayer.Id, "picture dropped into the layer is a layer background after the reload");
                    var dropped = BackgroundFactory.Instance.Get(LayerBackground);
                    Check(dropped != null && dropped.GetWidth() == FirstSize.X && dropped.GetHeight() == FirstSize.Y, "the layer background loads from the dropped file");
                    PressText("studio/map_new_scene");
                    break;
                case 23:
                    AnswerPrompt(NewScene);
                    break;
                case 24:
                    PickBackground(LayerBackground);
                    break;
                case 25:
                    var sceneFile = Path.Combine(Live.Layer.RootPath, "scenes", NewScene + ".json");
                    Check(File.Exists(sceneFile), "new scene written to the layer");
                    Check(File.Exists(sceneFile) && File.ReadAllText(sceneFile).Contains($"\"{LayerBackground}\""), "the new scene uses the picked background");
                    Check(GetNodeOrNull<SceneManager>("/root/SceneManager")?.HasScene(NewScene) == true, "new scene is known to the scene manager");
                    PressPayload(NewScene);
                    break;
                case 26:
                    if (SceneManager.GetCurrentSceneName() != NewScene || Box?.Locker is not ScenarioEvent || ShownBackgroundSize() != FirstSize)
                    {
                        _step--;
                        break;
                    }
                    Check(((ScenarioEvent)Box.Locker).SourceModId == StudioLayer.Id, $"the new scene's scenario runs from the layer ('{Box.Locker.ID}')");
                    Check(true, "the new scene shows the layer background");
                    Shot("new_scene");
                    WriteLayerBackground(SecondSize);
                    Press(Root + "Transport/Row/ReloadButton");
                    break;
                case 27:
                    Check(ShownBackgroundSize() == SecondSize, $"a replaced background is shown from the new file after the reload ({ShownBackgroundSize()})");
                    PressText("studio/map_export");
                    break;
                case 28:
                    AnswerPrompt(ExportId);
                    break;
                case 29:
                    var exported = Path.Combine(ProjectSettings.GlobalizePath("user://mods/"), ExportId);
                    Check(File.Exists(Path.Combine(exported, "manifest.json")) && File.Exists(Path.Combine(exported, "scenes", NewScene + ".json")), "layer exported as a mod with a manifest");
                    Check(File.Exists(Path.Combine(exported, "backgrounds", LayerBackground + ".png")), "the layer background is exported with the mod");
                    if (Directory.Exists(exported))
                        Directory.Delete(exported, true);
                    MenuPick(101);
                    break;
                case 30:
                    if (SceneManager.GetCurrentSceneName() != "home_kitchen")
                    {
                        _step--;
                        break;
                    }
                    Check(true, "back at the checkpoint's scene");
                    Press(Root + "Header/Row/CloseButton");
                    break;
                default:
                    Check(!_panel.Visible, "close hides the dock");
                    Cleanup();
                    Finish();
                    break;
            }
        }

        private static bool IsCurrentLine(StudioSession session, int number)
        {
            foreach (var line in session.Lines)
            {
                if (line.IsCurrent && line.Number == number)
                    return true;
            }
            return false;
        }

        private static bool IsRunningCandidate(StudioSession session, string name)
        {
            foreach (var candidate in session.Candidates)
            {
                if (candidate.Status == StudioCandidateStatus.Running && candidate.Name == name)
                    return true;
            }
            return false;
        }

        private static bool HasEditLog(StudioSession session)
        {
            foreach (var entry in session.Log)
            {
                if (entry.Category == StudioLogCategory.Edit)
                    return true;
            }
            return false;
        }

        private static bool HasNode(StudioSession session, StudioNodeState state, string name)
        {
            foreach (var node in session.Nodes)
            {
                if (node.State == state && node.Name == name)
                    return true;
            }
            return false;
        }

        private bool QuestionIsUp()
        {
            var tree = GetTree();
            if (!Boot.IsReady || tree.CurrentScene == null || UiHost.Instance == null || Live == null)
                return false;
            var box = Box;
            return box != null && box.IsRunning && box.Locker is ScenarioEvent && box.IsWaitingAnswer;
        }

        private Dialog.Action.IActionCommand CurrentAction()
        {
            var queue = Box?.Runtime.Dialog?.Queue;
            var index = Box?.Runtime.Index ?? -1;
            return queue != null && index >= 0 && index < queue.Count ? queue[index] : null;
        }

        private void Press(string path)
        {
            var button = _panel.GetNodeOrNull<Button>(path);
            Check(button != null, $"button {path} found");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void PressText(string key)
        {
            var button = FindButton(_panel, TranslationServer.Translate(key));
            Check(button != null, $"button '{key}' found");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void PressBrokenTarget()
        {
            foreach (var node in Content().GetChildren())
            {
                if (node is StudioBranchView { IsBroken: true } branch)
                {
                    var button = FindButton(branch, TranslationServer.Translate("studio/choose_target"));
                    Check(button != null, "broken answer offers 'choose where'");
                    button?.EmitSignal(BaseButton.SignalName.Pressed);
                    return;
                }
            }
            Fail("no broken answer to redirect");
        }

        private void PickScene(string scene)
        {
            PickItem(string.Format(TranslationServer.Translate("studio/target_scene"), scene), $"scene {scene} as a target");
        }

        private void PickBackground(string background)
        {
            PickItem(string.Format(TranslationServer.Translate("studio/background_item"), background, StudioLayer.Id), $"layer background {background}");
        }

        /// <summary>Picks the item <paramref name="caption"/> in the open studio pick list.</summary>
        private void PickItem(string caption, string what)
        {
            PopupMenu menu = null;
            foreach (var child in _panel.GetChildren())
            {
                if (child is PopupMenu popup && popup.Visible)
                    menu = popup;
            }
            Check(menu != null, $"pick list opened for {what}");
            if (menu == null)
                return;

            for (var i = 0; i < menu.ItemCount; i++)
            {
                if (menu.GetItemText(i) != caption)
                    continue;
                menu.EmitSignal(PopupMenu.SignalName.IdPressed, (long)menu.GetItemId(i));
                menu.Hide();
                return;
            }
            Fail($"not offered: {what}");
        }

        /// <summary>Writes a plain <paramref name="size"/> PNG as background <see cref="LayerBackground"/> of the test layer.</summary>
        private static void WriteLayerBackground(Vector2I size)
        {
            var path = Path.Combine(Live.Layer.RootPath, "backgrounds", LayerBackground + ".png");
            var image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgb8);
            image.Fill(new Color(0.2f, 0.4f, 0.3f));
            image.SavePng(path);
        }

        private static Vector2I ShownBackgroundSize()
        {
            var texture = (SceneContextNode.FindNode<Node>("ViewBox") as IBackgroundView)?.Background?.Texture;
            return texture == null ? Vector2I.Zero : new Vector2I(texture.GetWidth(), texture.GetHeight());
        }

        private void PressPayload(string payload)
        {
            foreach (var button in Descendants<StudioActionButton>(Content()))
            {
                if (button.Payload != payload)
                    continue;
                button.EmitSignal(BaseButton.SignalName.Pressed);
                return;
            }
            Fail($"no button for '{payload}'");
        }

        /// <summary>Types <paramref name="text"/> into the open studio prompt and confirms it.</summary>
        private void AnswerPrompt(string text)
        {
            foreach (var child in _panel.GetChildren())
            {
                if (child is not ConfirmationDialog dialog || !dialog.Visible)
                    continue;
                foreach (var field in Descendants<LineEdit>(dialog))
                    field.Text = text;
                dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
                return;
            }
            Fail("no studio prompt open");
        }

        private void ToggleBreakpoint(int line)
        {
            foreach (var node in Descendants<StudioCodeLineView>(Content()))
            {
                if (node.Line.SourceLine != line)
                    continue;
                var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
                node.Gutter.EmitSignal(Control.SignalName.GuiInput, click);
                return;
            }
            Fail($"no script line {line} in the author view");
        }

        private Rect2 FrameRect()
        {
            var frame = _panel.GetNode<Control>("Frame");
            return new Rect2(frame.Position, frame.Size);
        }

        /// <summary>Presses the left button on a dock handle, moves the mouse by <paramref name="delta"/> and releases.</summary>
        private void Drag(string path, Vector2 delta)
        {
            var handle = _panel.GetNodeOrNull<Control>(path);
            Check(handle != null, $"handle {path} found");
            if (handle != null)
                DragFrom(handle, delta);
        }

        private static void DragFrom(Control handle, Vector2 delta)
        {
            handle.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            handle.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = delta, ButtonMask = MouseButtonMask.Left });
            handle.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = delta });
        }

        /// <summary>Drags the ▶ marker of the current line onto the nearest line above it the dialog can run from.</summary>
        private void DragMarkerBack()
        {
            StudioCodeLineView current = null;
            StudioCodeLineView target = null;
            foreach (var view in Descendants<StudioCodeLineView>(Content()))
            {
                if (view.Line.IsCurrent)
                    current = view;
                else if (current == null && view.Line.CanRunNext)
                    target = view;
            }
            Check(current != null && target != null, "a current line and a runnable line above it");
            if (current == null || target == null)
                return;
            _movedLine = target.Line.SourceLine;
            DragFrom(current.Marker, target.GetGlobalRect().GetCenter() - current.Marker.GlobalPosition);
        }

        private void Restart(StudioRestartMode mode) => MenuPick((long)mode);

        private void MenuPick(long id)
        {
            var popup = _panel.GetNode<MenuButton>(Root + "Transport/Row/RestartButton").GetPopup();
            popup.EmitSignal(PopupMenu.SignalName.IdPressed, id);
        }

        private int Broken()
        {
            var broken = 0;
            foreach (var node in Content().GetChildren())
            {
                if (node is StudioBranchView { IsBroken: true })
                    broken++;
            }
            return broken;
        }

        private VBoxContainer Content() => _panel.GetNode<VBoxContainer>(Root + "Body/Scroll/Content");

        private int CountOf<T>() where T : Node => Descendants<T>(Content()).Count;

        private static System.Collections.Generic.List<T> Descendants<T>(Node node) where T : Node
        {
            var result = new System.Collections.Generic.List<T>();
            foreach (var child in node.GetChildren())
            {
                if (child is T typed)
                    result.Add(typed);
                result.AddRange(Descendants<T>(child));
            }
            return result;
        }

        private static Button FindButton(Node node, string text)
        {
            if (node is Button button && button.Text == text)
                return button;
            foreach (var child in node.GetChildren())
            {
                var found = FindButton(child, text);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void Cleanup()
        {
            var root = Live?.Layer.RootPath;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return;
            ModOverlays.Unmount(StudioLayer.Id);
            SceneManager.ReloadContent();
            Directory.Delete(root, true);
            var parent = Path.GetDirectoryName(root);
            if (parent != null && Directory.GetFileSystemEntries(parent).Length == 0)
                Directory.Delete(parent);
            Log($"test layer removed: {root}");
        }

        private void Shot(string id)
        {
            if (string.IsNullOrEmpty(_shotsDir) || DisplayServer.GetName() == "headless")
                return;

            var dir = ProjectSettings.GlobalizePath(_shotsDir);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, id + ".png");
            ViewportCapture.SavePng(GetViewport(), path);
            Log($"screenshot {path}");
        }

        private void Check(bool ok, string name)
        {
            if (ok)
            {
                _passed++;
                Log($"[PASS] {name}");
            }
            else
            {
                Fail(name);
            }
        }

        private void Fail(string message)
        {
            _failed++;
            Log($"[FAIL] {message}");
        }

        private void Finish()
        {
            if (_finished)
                return;
            _finished = true;
            GameLogger.Log("TEST", $"[TEST_RESULT] name={ResultName} passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private static void Log(string message) => GameLogger.Log("TEST", message);
    }
}
