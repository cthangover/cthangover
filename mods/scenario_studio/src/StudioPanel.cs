namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Base.Lists;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// The studio dock: a non-modal panel at the right edge over the running
    /// game (manifest <c>ui</c> entry <c>studio</c>, own toolbar icon
    /// <c>ui/icons/studio.png</c>). The root control spans the screen but
    /// ignores the mouse, so the game on the left keeps its input; only the
    /// <c>Dock</c> panel catches clicks. The dock sits in a <c>Frame</c> whose
    /// header and edge/corner grips move and resize it (<see cref="StudioDockDrag"/>);
    /// the geometry lives as long as the dock. The skeleton — header with the mode
    /// switch, transport row, "where we are", tab row, scrolling body and
    /// footer — lives in <c>scenes/studio_panel.tscn</c>; this behaviour dresses
    /// it with the studio styles and refills the body for the active tab and
    /// mode from a <see cref="StudioSession"/> built off <see cref="StudioLive"/>.
    /// <para/>
    /// The dock polls <see cref="StudioLive.Version"/> and redraws only when it
    /// changed and the user is not typing (a focused field, the inline text
    /// editor or an open prompt), so a running dialog never steals an edit.
    /// Every action goes through <see cref="StudioLive"/>; the dock keeps only
    /// view state (mode, tab, footer status) and the context of an open prompt.
    /// </summary>
    public partial class StudioPanel : Widget
    {
        private const string Root = "Frame/Dock/Column/";
        private const float PollSeconds = 0.4f;
        private const long TargetEnd = 0;
        private const long TargetLabelBase = 1000;
        private const long TargetSceneBase = 2000;
        private const int CheckpointSave = 100;
        private const int CheckpointRestore = 101;

        private readonly StudioNowPage _nowPage = new();
        private readonly StudioNovicePage _novicePage = new();
        private readonly List<StudioDockDrag> _drags = new();
        private StudioMode _mode = StudioMode.Author;
        private StudioTab _tab = StudioTab.Now;
        private StudioSession _session = new();
        private string _status;
        private int _shownVersion = -1;
        private CodeEdit _editor;
        private Node _prompt;

        private StudioCodeLine _promptLine;
        private List<string> _pickLabels = new();
        private List<string> _pickScenes = new();

        private PanelContainer _runState;
        private Label _runStateText;
        private Button _authorButton;
        private Button _noviceButton;
        private Button _pauseButton;
        private Button _stepButton;
        private Button _continueButton;
        private MenuButton _restartButton;
        private HFlowContainer _crumbs;
        private HFlowContainer _scope;
        private Button[] _tabs;
        private VBoxContainer _content;
        private Label _statusLabel;
        private Button _editButton;
        private Button _applyButton;

        /// <summary>Currently shown tab.</summary>
        public StudioTab Tab => _tab;

        /// <summary>Currently shown mode.</summary>
        public StudioMode Mode => _mode;

        /// <summary>The session the dock shows now.</summary>
        public StudioSession Session => _session;

        private static StudioLive Live => StudioLive.Instance;

        protected override void OnceConstruct()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            var frame = GetNode<Control>("Frame");
            var dock = GetNode<PanelContainer>("Frame/Dock");
            dock.AddThemeStyleboxOverride("panel", StudioStyles.Dock());
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>(Root + "Header"), StudioDockEdge.Move));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/LeftGrip"), StudioDockEdge.Left));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/RightGrip"), StudioDockEdge.Right));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/TopGrip"), StudioDockEdge.Top));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/BottomGrip"), StudioDockEdge.Bottom));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/TopLeftGrip"), StudioDockEdge.Top | StudioDockEdge.Left));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/TopRightGrip"), StudioDockEdge.Top | StudioDockEdge.Right));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/BottomLeftGrip"), StudioDockEdge.Bottom | StudioDockEdge.Left));
            _drags.Add(new StudioDockDrag(frame, dock, GetNode<Control>("Frame/BottomRightGrip"), StudioDockEdge.Bottom | StudioDockEdge.Right));
            foreach (var rule in new[] { "RuleHeader", "RuleTransport", "RuleContext", "RuleTabs", "RuleFooter" })
                GetNode<ColorRect>(Root + rule).Color = UiTokens.Line;

            StudioStyles.Title(GetNode<Label>(Root + "Header/Row/Title"), Tr("studio/title"));
            _runState = GetNode<PanelContainer>(Root + "Header/Row/RunState");
            _runStateText = GetNode<Label>(Root + "Header/Row/RunState/Text");

            GetNode<PanelContainer>(Root + "Header/Row/ModeSwitch").AddThemeStyleboxOverride("panel", StudioStyles.SegmentWell());
            _authorButton = GetNode<Button>(Root + "Header/Row/ModeSwitch/Items/AuthorButton");
            _noviceButton = GetNode<Button>(Root + "Header/Row/ModeSwitch/Items/NoviceButton");
            StudioStyles.Segment(_authorButton, Tr("studio/mode_author"));
            StudioStyles.Segment(_noviceButton, Tr("studio/mode_novice"));
            _authorButton.Pressed += OnAuthorPressed;
            _noviceButton.Pressed += OnNovicePressed;

            var close = GetNode<Button>(Root + "Header/Row/CloseButton");
            CthangoverTheme.ApplyIcon(close);
            close.TooltipText = Tr("studio/close");
            close.Pressed += OnClosePressed;

            _pauseButton = GetNode<Button>(Root + "Transport/Row/PauseButton");
            StudioStyles.Transport(_pauseButton, Tr("studio/pause"));
            _pauseButton.Pressed += OnPausePressed;
            _stepButton = GetNode<Button>(Root + "Transport/Row/StepButton");
            StudioStyles.Transport(_stepButton, Tr("studio/step"));
            _stepButton.Pressed += OnStepPressed;
            _continueButton = GetNode<Button>(Root + "Transport/Row/ContinueButton");
            StudioStyles.Transport(_continueButton, Tr("studio/continue"));
            _continueButton.Pressed += OnContinuePressed;
            _restartButton = GetNode<MenuButton>(Root + "Transport/Row/RestartButton");
            BuildRestartMenu(_restartButton);

            _crumbs = GetNode<HFlowContainer>(Root + "Context/Column/Crumbs");
            _scope = GetNode<HFlowContainer>(Root + "Context/Column/Scope");

            _tabs = new[]
            {
                GetNode<Button>(Root + "Tabs/Row/NowTab"),
                GetNode<Button>(Root + "Tabs/Row/SceneTab"),
                GetNode<Button>(Root + "Tabs/Row/StateTab"),
                GetNode<Button>(Root + "Tabs/Row/LogTab"),
                GetNode<Button>(Root + "Tabs/Row/MapTab"),
            };
            StudioStyles.SmallTab(_tabs[(int)StudioTab.Now], Tr("studio/tab_now"));
            StudioStyles.SmallTab(_tabs[(int)StudioTab.Scene], Tr("studio/tab_scene"));
            StudioStyles.SmallTab(_tabs[(int)StudioTab.State], Tr("studio/tab_state"));
            StudioStyles.SmallTab(_tabs[(int)StudioTab.Log], Tr("studio/tab_log"));
            StudioStyles.SmallTab(_tabs[(int)StudioTab.Map], Tr("studio/tab_map"));
            _tabs[(int)StudioTab.Now].Pressed += OnNowTabPressed;
            _tabs[(int)StudioTab.Scene].Pressed += OnSceneTabPressed;
            _tabs[(int)StudioTab.State].Pressed += OnStateTabPressed;
            _tabs[(int)StudioTab.Log].Pressed += OnLogTabPressed;
            _tabs[(int)StudioTab.Map].Pressed += OnMapTabPressed;

            _content = GetNode<VBoxContainer>(Root + "Body/Scroll/Content");
            _nowPage.BreakpointToggled += OnBreakpointToggled;
            _nowPage.InsertRequested += OnInsertRequested;
            _nowPage.NextLineRequested += OnNextLineRequested;
            _novicePage.LineRequested += OnNoviceLineRequested;
            _novicePage.OptionRequested += OnNoviceOptionRequested;
            _novicePage.EditRequested += OnEditLineRequested;
            _novicePage.TargetRequested += OnTargetRequested;

            _statusLabel = GetNode<Label>(Root + "Footer/Row/Status");
            _statusLabel.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            _statusLabel.AddThemeColorOverride("font_color", UiTokens.Faint);
            _editButton = GetNode<Button>(Root + "Footer/Row/EditButton");
            StudioStyles.SmallButton(_editButton, Tr("studio/edit_text"));
            _editButton.Pressed += OnEditPressed;
            _applyButton = GetNode<Button>(Root + "Footer/Row/ApplyButton");
            StudioStyles.SmallButton(_applyButton, Tr("studio/apply"), primary: true);
            _applyButton.Pressed += OnApplyPressed;

            var timer = new Timer { WaitTime = PollSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += OnPoll;

            Refresh();
        }

        /// <summary>Redraws at once whenever the dock is shown.</summary>
        protected override void ShowConstruct()
        {
            _shownVersion = -1;
            if (_content != null)
                Refresh();
        }

        private void BuildRestartMenu(MenuButton button)
        {
            StudioStyles.Transport(button, Tr("studio/restart"));
            var popup = button.GetPopup();
            CthangoverTheme.Apply(popup);
            popup.AddItem(Tr("studio/restart_scenario"), (int)StudioRestartMode.Scenario);
            popup.AddItem(Tr("studio/restart_rollback"), (int)StudioRestartMode.Rollback);
            popup.AddItem(Tr("studio/restart_scene"), (int)StudioRestartMode.Scene);
            popup.AddItem(Tr("studio/restart_from_label"), (int)StudioRestartMode.FromLabel);
            popup.AddSeparator(Tr("studio/checkpoint"));
            popup.AddItem(Tr("studio/checkpoint_save"), CheckpointSave);
            popup.AddItem(Tr("studio/checkpoint_restore"), CheckpointRestore);
            popup.IdPressed += OnRestartPicked;
        }

        // ── drawing ────────────────────────────────────────────────

        private void OnPoll()
        {
            if (!Visible || Live == null || Live.Version == _shownVersion || IsBusy())
                return;
            Refresh();
        }

        /// <summary>True while the user types, answers a prompt or drags the ▶ marker: redrawing would drop it.</summary>
        private bool IsBusy()
        {
            if (_editor != null || _nowPage.IsDragging || (_prompt != null && IsInstanceValid(_prompt)))
                return true;
            var focus = GetViewport()?.GuiGetFocusOwner();
            return focus is LineEdit or TextEdit && _content.IsAncestorOf(focus);
        }

        /// <summary>Rebuilds the session from the live game and re-renders every dynamic part of the dock.</summary>
        public void Refresh()
        {
            if (Live == null)
                return;

            _shownVersion = Live.Version;
            _session = StudioSessionBuilder.Build(Live);

            RefreshHeader();
            RefreshContext();
            RefreshBody();
            RefreshFooter();
        }

        private void RefreshHeader()
        {
            var state = _session.RunState;
            var stopped = state == StudioRunState.Paused || state == StudioRunState.Held;
            StudioStyles.RunState(_runState, _runStateText, state, Tr(RunStateKey(state)));
            _pauseButton.SetPressedNoSignal(Live.Debugger.IsPaused);
            _stepButton.Disabled = !_session.HasScenario;
            _continueButton.Disabled = !stopped;
            var restartMenu = _restartButton.GetPopup();
            for (var i = 0; i < restartMenu.ItemCount; i++)
            {
                var id = restartMenu.GetItemId(i);
                if (id < CheckpointSave && !restartMenu.IsItemSeparator(i))
                    restartMenu.SetItemDisabled(i, !_session.HasScenario);
                else if (id == CheckpointRestore)
                    restartMenu.SetItemDisabled(i, !Live.HasCheckpoint);
            }
            _authorButton.SetPressedNoSignal(_mode == StudioMode.Author);
            _noviceButton.SetPressedNoSignal(_mode == StudioMode.Novice);
            for (var i = 0; i < _tabs.Length; i++)
                _tabs[i].SetPressedNoSignal(i == (int)_tab);
            _tabs[(int)StudioTab.Scene].Text = _session.Candidates.Count > 0
                ? $"{Tr("studio/tab_scene")}  {_session.Candidates.Count}"
                : Tr("studio/tab_scene");
        }

        private static string RunStateKey(StudioRunState state)
        {
            switch (state)
            {
                case StudioRunState.Running:
                    return "studio/state_running";
                case StudioRunState.Paused:
                    return "studio/state_paused";
                case StudioRunState.Held:
                    return "studio/state_held";
                default:
                    return "studio/state_idle";
            }
        }

        private void RefreshContext()
        {
            Clear(_crumbs);
            AddCrumb(Tr("studio/crumb_scene"), _session.SceneName ?? "—");
            if (_session.HasScenario)
                AddCrumb(Tr("studio/crumb_scenario"), _session.ScenarioName);
            if (!string.IsNullOrEmpty(_session.LabelName))
                AddCrumb(null, _session.LabelName);

            Clear(_scope);
            if (_session.FileScope == null)
                return;
            _scope.AddChild(StudioStyles.Scope(_session.FileScope));
            if (_session.EditScope != null && _session.EditScope.ModId != _session.FileScope.ModId)
            {
                var hint = StudioStyles.Text(string.Format(Tr("studio/edits_go_to"), _session.EditScope.ModId), UiTokens.FontSmall, UiTokens.Faint);
                hint.VerticalAlignment = VerticalAlignment.Center;
                _scope.AddChild(hint);
            }
        }

        private void AddCrumb(string key, string value)
        {
            if (_crumbs.GetChildCount() > 0)
                _crumbs.AddChild(StudioStyles.Text("›", UiTokens.FontBodySmall, UiTokens.Faint));
            if (key != null)
                _crumbs.AddChild(StudioStyles.Text(key, UiTokens.FontSmall, UiTokens.Faint));
            _crumbs.AddChild(StudioStyles.Text(value, UiTokens.FontBodySmall, UiTokens.Body, mono: true));
        }

        private void RefreshBody()
        {
            if (_editor != null)
                return;

            Clear(_content);
            switch (_tab)
            {
                case StudioTab.Now when _mode == StudioMode.Author:
                    _nowPage.Build(_content, _session);
                    break;
                case StudioTab.Now:
                    _novicePage.Build(_content, _session);
                    break;
                case StudioTab.Scene:
                    StudioScenePage.Build(_content, _session, OnRunCandidate, OnResetCandidate, OnNewScenario);
                    break;
                case StudioTab.State:
                    StudioStatePage.Build(_content, _session, OnStateSubmitted);
                    break;
                case StudioTab.Log:
                    StudioLogPage.Build(_content, _session);
                    break;
                case StudioTab.Map:
                    StudioMapPage.Build(_content, _session, OnGoToScene, OnNewScene, OnLinkScene, OnExport);
                    break;
            }
        }

        private void RefreshFooter()
        {
            var pending = _session.PendingCount;
            _statusLabel.Text = _status ?? (pending == 0 ? Tr("studio/no_pending") : string.Format(Tr("studio/pending"), pending));
            _applyButton.Disabled = pending == 0 && _editor == null;
            _editButton.Visible = _mode == StudioMode.Author;
            _editButton.Disabled = !_session.HasScenario;
            _editButton.Text = Tr(_editor != null ? "studio/edit_done" : "studio/edit_text");
        }

        private static void Clear(Node container)
        {
            foreach (var child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }
        }

        private void SetStatus(string text)
        {
            _status = text;
            _statusLabel.Text = text;
        }

        private void Changed()
        {
            _status = null;
            Refresh();
        }

        // ── header, tabs, transport ────────────────────────────────

        private void ShowTab(StudioTab tab)
        {
            CloseEditor();
            _tab = tab;
            Changed();
        }

        private void ShowMode(StudioMode mode)
        {
            CloseEditor();
            _mode = mode;
            _tab = StudioTab.Now;
            Changed();
        }

        private void OnAuthorPressed() => ShowMode(StudioMode.Author);

        private void OnNovicePressed() => ShowMode(StudioMode.Novice);

        private void OnNowTabPressed() => ShowTab(StudioTab.Now);

        private void OnSceneTabPressed() => ShowTab(StudioTab.Scene);

        private void OnStateTabPressed() => ShowTab(StudioTab.State);

        private void OnLogTabPressed() => ShowTab(StudioTab.Log);

        private void OnMapTabPressed() => ShowTab(StudioTab.Map);

        private void OnClosePressed() => Hide();

        private void OnPausePressed()
        {
            if (Live.Debugger.IsPaused)
                Live.Debugger.Continue(Live.Box);
            else
                Live.Debugger.Pause();
            Changed();
        }

        private void OnStepPressed()
        {
            var box = Live.Box;
            if (box != null && box.IsWaitingAnswer && !box.Runtime.IsHeld)
            {
                SetStatus(Tr("studio/step_needs_answer"));
                return;
            }
            Live.Debugger.Step(box);
            Changed();
        }

        private void OnContinuePressed()
        {
            Live.Debugger.Continue(Live.Box);
            Changed();
        }

        private void OnRestartPicked(long id)
        {
            CloseEditor();
            switch (id)
            {
                case CheckpointSave:
                    Live.SaveCheckpoint();
                    _status = Tr("studio/checkpoint_saved");
                    Refresh();
                    return;
                case CheckpointRestore:
                    _status = Live.RestoreCheckpoint() ? Tr("studio/checkpoint_restored") : Tr("studio/checkpoint_none");
                    Refresh();
                    return;
                default:
                    Live.Restart((StudioRestartMode)id);
                    Changed();
                    return;
            }
        }

        // ── author edits ───────────────────────────────────────────

        private void OnBreakpointToggled(StudioCodeLine line)
        {
            if (line.SourceLine > 0)
                Live.Debugger.ToggleBreakpoint(_session.ScenarioName, line.SourceLine);
            Changed();
        }

        private void OnNextLineRequested(StudioCodeLine line)
        {
            if (Live.SetNextLine(line.SourceLine))
                Changed();
            else
                SetStatus(Tr("studio/next_line_failed"));
        }

        private void OnInsertRequested(StudioLineKind kind)
        {
            var at = _session.InsertionTextIndexAfterCurrent();
            switch (kind)
            {
                case StudioLineKind.Choice:
                    Live.Insert(at, StudioScriptEdits.NewQuestion(), StudioScriptEdits.NewAnswer());
                    Changed();
                    break;
                case StudioLineKind.Jump:
                    _promptLine = new StudioCodeLine { Number = at };
                    OpenScenePick(OnInsertJumpPicked);
                    break;
                default:
                    Live.Insert(at, StudioScriptEdits.NewLine());
                    Changed();
                    break;
            }
        }

        private void OnInsertJumpPicked(long id)
        {
            var scene = PickedScene(id);
            if (scene != null && _promptLine != null)
                Live.Insert(_promptLine.Number, StudioScriptEdits.Jump(scene));
            _prompt = null;
            Changed();
        }

        private void OnEditPressed()
        {
            if (_editor != null)
            {
                CloseEditor();
                Changed();
                return;
            }

            var copy = Live.EnsureCopy();
            if (copy == null)
                return;
            _tab = StudioTab.Now;
            Clear(_content);
            _editor = StudioNowPage.BuildEditor(_content, copy.Text);
            _editor.GrabFocus();
            RefreshFooter();
        }

        /// <summary>Takes the inline editor's text into the working copy and closes it.</summary>
        private void CloseEditor()
        {
            if (_editor == null)
                return;
            var text = _editor.Text;
            _editor = null;
            Live.ReplaceText(text);
        }

        private void OnApplyPressed()
        {
            CloseEditor();
            var scope = Live.Apply();
            _status = scope == null ? Tr("studio/no_pending") : string.Format(Tr("studio/applied_to"), scope.ModId);
            Refresh();
        }

        // ── novice edits ───────────────────────────────────────────

        private void OnNoviceLineRequested(int at)
        {
            Live.Insert(at, StudioScriptEdits.NewLine());
            Changed();
        }

        private void OnNoviceOptionRequested(int at)
        {
            Live.Insert(at, StudioScriptEdits.NewAnswer());
            Changed();
        }

        private void OnEditLineRequested(StudioActionButton button)
        {
            _promptLine = button.Line;
            _prompt = StudioPrompt.AskText(this, Tr("studio/prompt_edit_title"), button.Line.Say?.Resolve(), OnLineTextEntered);
        }

        private void OnLineTextEntered(string text)
        {
            if (_promptLine != null && !string.IsNullOrWhiteSpace(text))
            {
                var index = _promptLine.Number - 1;
                var copy = Live.EnsureCopy();
                if (copy != null)
                    Live.ReplaceLine(index, StudioScriptEdits.WithSay(copy.Line(index), text.Trim()));
            }
            _prompt = null;
            Changed();
        }

        /// <summary>"Choose where" for an answer: the end, the scenario's labels or any scene.</summary>
        private void OnTargetRequested(StudioActionButton button)
        {
            _promptLine = button.Line;
            var menu = StudioPrompt.Pick(this, OnTargetPicked);
            menu.AddItem(Tr("studio/target_end"), (int)TargetEnd);

            _pickLabels = new List<string>();
            foreach (var line in _session.Lines)
            {
                if (line.Kind == StudioLineKind.Label)
                    _pickLabels.Add(line.Target);
            }
            if (_pickLabels.Count > 0)
                menu.AddSeparator(Tr("studio/target_labels"));
            for (var i = 0; i < _pickLabels.Count; i++)
                menu.AddItem(":" + _pickLabels[i], (int)(TargetLabelBase + i));

            AddScenes(menu);
            _prompt = menu;
            StudioPrompt.Open(menu);
        }

        private void OnTargetPicked(long id)
        {
            if (_promptLine != null)
            {
                var index = _promptLine.Number - 1;
                if (id == TargetEnd)
                    Live.TargetEnd(index);
                else if (id >= TargetSceneBase)
                    Live.TargetScene(index, PickedScene(id));
                else if (id >= TargetLabelBase && id - TargetLabelBase < _pickLabels.Count)
                    Live.TargetLabel(index, _pickLabels[(int)(id - TargetLabelBase)]);
            }
            _prompt = null;
            Changed();
        }

        private void OpenScenePick(PopupMenu.IdPressedEventHandler picked)
        {
            var menu = StudioPrompt.Pick(this, picked);
            AddScenes(menu);
            _prompt = menu;
            StudioPrompt.Open(menu);
        }

        private void AddScenes(PopupMenu menu)
        {
            _pickScenes = new List<string>();
            var scenes = AppServices.Current.SceneDefs.CollectAll();
            if (scenes != null)
                _pickScenes.AddRange(scenes.Keys);
            _pickScenes.Sort(System.StringComparer.Ordinal);
            menu.AddSeparator(Tr("studio/target_scenes"));
            for (var i = 0; i < _pickScenes.Count; i++)
                menu.AddItem(string.Format(Tr("studio/target_scene"), _pickScenes[i]), (int)(TargetSceneBase + i));
        }

        private string PickedScene(long id)
        {
            var index = (int)(id - TargetSceneBase);
            return index >= 0 && index < _pickScenes.Count ? _pickScenes[index] : null;
        }

        // ── scene, state, map ──────────────────────────────────────

        private void OnRunCandidate(StudioActionButton button)
        {
            var started = Live.RunNow(button.Payload);
            _tab = StudioTab.Now;
            _status = started ? null : Tr("studio/run_failed");
            Refresh();
        }

        private void OnResetCandidate(StudioActionButton button)
        {
            Live.ForgetCompleted(button.Payload);
            Changed();
        }

        private void OnNewScenario(StudioActionButton button)
        {
            var name = Live.CreateScenario();
            _status = name == null ? null : string.Format(Tr("studio/scenario_created"), name);
            Refresh();
        }

        private void OnStateSubmitted(StudioStateEntry entry, string value)
        {
            GetViewport()?.GuiReleaseFocus();
            Live.EditState(entry, value);
            Changed();
        }

        private void OnGoToScene(StudioActionButton button)
        {
            Live.GoToScene(button.Payload);
            Changed();
        }

        private void OnNewScene(StudioActionButton button)
        {
            _prompt = StudioPrompt.AskText(this, Tr("studio/prompt_scene_title"), "", OnSceneNameEntered);
        }

        private void OnSceneNameEntered(string name)
        {
            _prompt = null;
            _status = Live.CreateScene(name) ? string.Format(Tr("studio/scene_created"), name.Trim().ToLowerInvariant()) : Tr("studio/bad_name");
            Refresh();
        }

        private void OnLinkScene(StudioActionButton button)
        {
            _promptLine = new StudioCodeLine { Number = _session.InsertionTextIndexAfterCurrent() };
            OpenScenePick(OnInsertJumpPicked);
        }

        private void OnExport(StudioActionButton button)
        {
            _prompt = StudioPrompt.AskText(this, Tr("studio/prompt_export_title"), "my_story", OnExportEntered);
        }

        private void OnExportEntered(string modId)
        {
            _prompt = null;
            var path = Live.Export(modId);
            _status = path == null ? Tr("studio/bad_name") : string.Format(Tr("studio/exported"), modId.Trim().ToLowerInvariant());
            Refresh();
        }
    }
}
