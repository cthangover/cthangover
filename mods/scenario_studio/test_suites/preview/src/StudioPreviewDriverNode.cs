namespace Cthangover.Mods.ScenarioStudio.Tests.Preview
{
    using System.IO;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.UI;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Drives the studio dock the way a player would: toggles the <c>studio</c>
    /// UI entry (what the toolbar button does), then presses the dock's own
    /// buttons — "+ Line" in the author mode, the novice mode, every tab, close —
    /// one step per tick. After each step it checks what the step must have
    /// changed and optionally saves a screenshot named after the state. Ends
    /// with <c>[TEST_RESULT] name=studio_preview passed=N failed=M</c>.
    /// </summary>
    public partial class StudioPreviewDriverNode : Node
    {
        private const string ResultName = "studio_preview";
        private const string EntryId = "studio";
        private const string Root = "Dock/Column/";
        private const float TickSeconds = 0.5f;
        private const float TimeoutSeconds = 150f;

        private readonly string _shotsDir;
        private StudioPanel _panel;
        private int _step = -1;
        private int _passed;
        private int _failed;
        private double _elapsed;
        private bool _finished;

        public StudioPreviewDriverNode(string shotsDir)
        {
            _shotsDir = shotsDir;
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

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

            if (_step < 0)
            {
                var tree = GetTree();
                if (!Boot.IsReady || tree.CurrentScene == null || UiHost.Instance == null)
                    return;
                if (SceneContextNode.FindNode<Node>("TopView") == null)
                    return;
            }

            _step++;
            switch (_step)
            {
                case 0:
                    AppServices.Current.Ui.ToggleEntry(EntryId);
                    break;
                case 1:
                    CheckOpened();
                    Shot("now_author");
                    Press(FindButton(_panel, TranslationServer.Translate("studio/add_line")));
                    break;
                case 2:
                    Check(CountOf<StudioCodeLineView>(Content()) == 12, "author: inserted line rendered (12 lines)");
                    Check(Mathf.Abs(_panel.GetNode<Control>("Dock").Size.X - StudioStyles.DockWidth) < 1, "dock width unchanged after the insert");
                    Check(Status().Text.Contains("1"), $"footer shows one pending change: '{Status().Text}'");
                    Shot("now_author_inserted");
                    Press(_panel.GetNode<Button>(Root + "Header/Row/ModeSwitch/Items/NoviceButton"));
                    break;
                case 3:
                    Check(_panel.Mode == StudioMode.Novice, "novice mode selected");
                    Check(CountOf<StudioBlockView>(Content()) == 12, "novice: one card per line");
                    Check(CountOf<StudioCodeLineView>(Content()) == 0, "novice: no script lines");
                    Shot("now_novice");
                    Press(_panel.GetNode<Button>(Root + "Header/Row/ModeSwitch/Items/AuthorButton"));
                    Press(_panel.GetNode<Button>(Root + "Tabs/Row/SceneTab"));
                    break;
                case 4:
                    CheckTab(StudioTab.Scene, "scene");
                    Press(_panel.GetNode<Button>(Root + "Tabs/Row/StateTab"));
                    break;
                case 5:
                    CheckTab(StudioTab.State, "state");
                    Check(CountOf<LineEdit>(Content()) > 0, "state: editable values");
                    Press(_panel.GetNode<Button>(Root + "Tabs/Row/LogTab"));
                    break;
                case 6:
                    CheckTab(StudioTab.Log, "log");
                    Press(_panel.GetNode<Button>(Root + "Tabs/Row/MapTab"));
                    break;
                case 7:
                    CheckTab(StudioTab.Map, "map");
                    Press(_panel.GetNode<Button>(Root + "Header/Row/CloseButton"));
                    break;
                default:
                    Check(!_panel.Visible, "close button hides the dock");
                    Finish();
                    break;
            }
        }

        private void CheckOpened()
        {
            Check(UiHost.Instance.TryGetInstance(EntryId, out var control), "studio entry instantiated by the toggle");
            _panel = control as StudioPanel;
            Check(_panel != null && _panel.Visible, "studio dock visible");
            if (_panel == null)
            {
                Finish();
                return;
            }

            Check(_panel.MouseFilter == Control.MouseFilterEnum.Ignore, "dock root lets the game take the mouse");
            var dock = _panel.GetNode<Control>("Dock").GetGlobalRect();
            var viewport = GetViewport().GetVisibleRect().Size;
            Check(dock.End.X <= viewport.X && dock.Position.X > viewport.X / 2, $"dock {dock} sits at the right edge of {viewport}");
            Check(dock.Position.Y >= 84, $"dock {dock} starts below the HUD row");
            Check(Mathf.Abs(dock.Size.X - StudioStyles.DockWidth) < 1, $"dock keeps its {StudioStyles.DockWidth}px width ({dock.Size.X})");
            Check(_panel.Tab == StudioTab.Now && _panel.Mode == StudioMode.Author, "opens on Now in the author mode");
            Check(CountOf<StudioCodeLineView>(Content()) == 11, "author: one script line per stub line");
        }

        private void CheckTab(StudioTab tab, string name)
        {
            Check(_panel.Tab == tab, $"{name}: tab selected");
            Check(Content().GetChildCount() > 0, $"{name}: body filled");
            Shot("tab_" + name);
        }

        private VBoxContainer Content() => _panel.GetNode<VBoxContainer>(Root + "Body/Scroll/Content");

        private Label Status() => _panel.GetNode<Label>(Root + "Footer/Row/Status");

        private void Press(Button button)
        {
            Check(button != null, "button found");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
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

        private static int CountOf<T>(Node node) where T : Node
        {
            var count = node is T ? 1 : 0;
            foreach (var child in node.GetChildren())
                count += CountOf<T>(child);
            return count;
        }

        private void Shot(string id)
        {
            if (string.IsNullOrEmpty(_shotsDir) || DisplayServer.GetName() == "headless")
                return;

            var dir = ProjectSettings.GlobalizePath(_shotsDir);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, id + ".png");
            GetViewport().GetTexture().GetImage().SavePng(path);
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
