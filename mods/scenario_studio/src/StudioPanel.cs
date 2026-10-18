namespace Cthangover.Mods.ScenarioStudio
{
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Base.Lists;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// The studio dock: a non-modal panel at the right edge over the running
    /// game (manifest <c>ui</c> entry <c>studio</c>, own toolbar icon <c>ui/icons/studio.png</c>). The
    /// root control spans the screen but ignores the mouse, so the game on the
    /// left keeps its input; only the <c>Dock</c> panel catches clicks. The
    /// skeleton — header with the mode switch, transport row, "where we are",
    /// tab row, scrolling body and footer — lives in
    /// <c>scenes/studio_panel.tscn</c>; this behaviour dresses it with the
    /// studio styles and refills the body for the active tab and mode.
    /// <para/>
    /// Prototype stage: the dock renders <see cref="StudioSession.CreateStub"/>.
    /// Pause, step, breakpoints and the insert buttons only move markers in
    /// that stub; actions that would touch the game report "not wired" in the
    /// footer. The look and the flow are what is being agreed here.
    /// </summary>
    public partial class StudioPanel : Widget
    {
        private const string Root = "Dock/Column/";
        private const string StubTime = "00:44";

        private readonly StudioSession _session = StudioSession.CreateStub();
        private readonly StudioNowPage _nowPage = new();
        private readonly StudioNovicePage _novicePage = new();
        private StudioMode _mode = StudioMode.Author;
        private StudioTab _tab = StudioTab.Now;
        private string _status;

        private PanelContainer _runState;
        private Label _runStateText;
        private Button _authorButton;
        private Button _noviceButton;
        private Button _pauseButton;
        private HFlowContainer _crumbs;
        private HFlowContainer _scope;
        private Button[] _tabs;
        private VBoxContainer _content;
        private Label _statusLabel;

        /// <summary>Currently shown tab; exposed for the preview test.</summary>
        public StudioTab Tab => _tab;

        /// <summary>Currently shown mode; exposed for the preview test.</summary>
        public StudioMode Mode => _mode;

        protected override void OnceConstruct()
        {
            MouseFilter = MouseFilterEnum.Ignore;

            var dock = GetNode<PanelContainer>("Dock");
            dock.AddThemeStyleboxOverride("panel", StudioStyles.Dock());
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
            var step = GetNode<Button>(Root + "Transport/Row/StepButton");
            StudioStyles.Transport(step, Tr("studio/step"));
            step.Pressed += OnStepPressed;
            var resume = GetNode<Button>(Root + "Transport/Row/ContinueButton");
            StudioStyles.Transport(resume, Tr("studio/continue"));
            resume.Pressed += OnContinuePressed;
            BuildRestartMenu(GetNode<MenuButton>(Root + "Transport/Row/RestartButton"));

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
            StudioStyles.SmallTab(_tabs[(int)StudioTab.Scene], $"{Tr("studio/tab_scene")}  {_session.Candidates.Count}");
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
            _novicePage.LineRequested += OnNoviceLineRequested;
            _novicePage.OptionRequested += OnNoviceOptionRequested;

            _statusLabel = GetNode<Label>(Root + "Footer/Row/Status");
            _statusLabel.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            _statusLabel.AddThemeColorOverride("font_color", UiTokens.Faint);
            var edit = GetNode<Button>(Root + "Footer/Row/EditButton");
            StudioStyles.SmallButton(edit, Tr("studio/edit_text"));
            edit.Pressed += OnEditPressed;
            var apply = GetNode<Button>(Root + "Footer/Row/ApplyButton");
            StudioStyles.SmallButton(apply, Tr("studio/apply"), primary: true);
            apply.Pressed += OnApplyPressed;

            Refresh();
        }

        private void BuildRestartMenu(MenuButton button)
        {
            StudioStyles.Transport(button, Tr("studio/restart"));
            var popup = button.GetPopup();
            popup.AddItem(Tr("studio/restart_scenario"), 0);
            popup.AddItem(Tr("studio/restart_rollback"), 1);
            popup.AddItem(Tr("studio/restart_scene"), 2);
            popup.AddItem(Tr("studio/restart_from_label"), 3);
            popup.IdPressed += OnRestartPicked;
        }

        /// <summary>Re-renders every dynamic part of the dock from the session, the mode and the tab.</summary>
        private void Refresh()
        {
            StudioStyles.RunState(_runState, _runStateText, _session.IsPaused,
                Tr(_session.IsPaused ? "studio/state_paused" : "studio/state_running"));
            _pauseButton.SetPressedNoSignal(_session.IsPaused);
            _authorButton.SetPressedNoSignal(_mode == StudioMode.Author);
            _noviceButton.SetPressedNoSignal(_mode == StudioMode.Novice);
            for (var i = 0; i < _tabs.Length; i++)
                _tabs[i].SetPressedNoSignal(i == (int)_tab);

            RefreshContext();
            RefreshBody();
            _statusLabel.Text = _status ?? PendingText();
        }

        private void RefreshContext()
        {
            Clear(_crumbs);
            AddCrumb(Tr("studio/crumb_scene"), _session.SceneName);
            AddCrumb(Tr("studio/crumb_scenario"), _session.ScenarioName);
            if (!string.IsNullOrEmpty(_session.LabelName))
                AddCrumb(null, _session.LabelName);

            Clear(_scope);
            _scope.AddChild(StudioStyles.Scope(_session.FileScope));
            if (_session.EditScope.ModId != _session.FileScope.ModId)
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
            Clear(_content);
            switch (_tab)
            {
                case StudioTab.Now when _mode == StudioMode.Author:
                    _nowPage.Build(_content, _session);
                    break;
                case StudioTab.Now:
                    _novicePage.Build(_content, _session, OnPrototypeAction);
                    break;
                case StudioTab.Scene:
                    StudioScenePage.Build(_content, _session, OnPrototypeAction);
                    break;
                case StudioTab.State:
                    StudioStatePage.Build(_content, _session);
                    break;
                case StudioTab.Log:
                    StudioLogPage.Build(_content, _session);
                    break;
                case StudioTab.Map:
                    StudioMapPage.Build(_content, _session, OnPrototypeAction);
                    break;
            }
        }

        private string PendingText()
        {
            var pending = _session.PendingCount;
            return pending == 0 ? Tr("studio/no_pending") : string.Format(Tr("studio/pending"), pending);
        }

        private static void Clear(Node container)
        {
            foreach (var child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }
        }

        private void ShowTab(StudioTab tab)
        {
            _tab = tab;
            _status = null;
            Refresh();
        }

        private void ShowMode(StudioMode mode)
        {
            _mode = mode;
            _tab = StudioTab.Now;
            _status = null;
            Refresh();
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
            _session.IsPaused = !_session.IsPaused;
            Refresh();
        }

        private void OnStepPressed()
        {
            _session.Step();
            Refresh();
        }

        private void OnContinuePressed()
        {
            _session.IsPaused = false;
            Refresh();
        }

        private void OnBreakpointToggled(StudioCodeLine line) => Refresh();

        private void OnInsertRequested(StudioLineKind kind)
        {
            InsertLine(_session.InsertionIndexAfterCurrent(), NewLine(kind));
        }

        private void OnNoviceLineRequested(int at) => InsertLine(at, NewLine(StudioLineKind.Line));

        private void OnNoviceOptionRequested(int at) => InsertLine(at, NewLine(StudioLineKind.Option));

        private void InsertLine(int at, StudioCodeLine line)
        {
            _session.InsertAt(at, line);
            _status = null;
            Refresh();
        }

        /// <summary>
        /// A fresh line of the given kind as the studio inserts it: DSL written in
        /// the current language (what an author would type) plus the structure the
        /// novice view reads. A new answer has no target yet, so it shows as broken
        /// until one is chosen.
        /// </summary>
        private StudioCodeLine NewLine(StudioLineKind kind)
        {
            switch (kind)
            {
                case StudioLineKind.Choice:
                    return new StudioCodeLine { Kind = kind, Text = $"select \"{Tr("studio/new_choice_text")}\"", Say = new StudioText("studio/new_choice_text") };
                case StudioLineKind.Option:
                    return new StudioCodeLine { Kind = kind, Text = $"option \"{Tr("studio/new_option_text")}\"", Say = new StudioText("studio/new_option_text") };
                case StudioLineKind.Jump:
                    return new StudioCodeLine { Kind = kind, Text = "switch_scene yard", Target = "yard" };
                default:
                    return new StudioCodeLine { Kind = StudioLineKind.Line, Text = $"text \"{Tr("studio/new_line_text")}\"", Say = new StudioText("studio/new_line_text") };
            }
        }

        private void OnEditPressed() => OnPrototypeAction(Tr("studio/edit_text"));

        private void OnApplyPressed()
        {
            _session.Apply(StubTime);
            _status = Tr("studio/applied");
            Refresh();
        }

        private void OnRestartPicked(long id)
        {
            var popup = GetNode<MenuButton>(Root + "Transport/Row/RestartButton").GetPopup();
            OnPrototypeAction(popup.GetItemText(popup.GetItemIndex((int)id)));
        }

        private void OnPrototypeAction(string caption)
        {
            _status = string.Format(Tr("studio/picked"), caption);
            _statusLabel.Text = _status;
        }
    }
}
