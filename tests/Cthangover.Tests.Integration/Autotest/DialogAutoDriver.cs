#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Auto-plays dialog scenes for integration tests and the AI workflow.
    /// Attached by <c>SceneSwitcher</c> and <c>MainMenuTestSetup</c>, it clicks
    /// <c>NextAction</c> at a fixed interval and answers selection prompts from
    /// <c>--choices=</c> (first variant when unspecified). The DialogBox is
    /// discovered lazily through the core <see cref="IDialogBox"/> contract, so
    /// the driver never references the dialog_base mod assembly; end-of-dialog
    /// quits the process, optionally switching to <c>--scene2=</c> first.
    /// </summary>
    public partial class DialogAutoDriver : Node
    {
        [Export] public float AdvanceInterval { get; set; } = 0.1f;
        [Export] public bool AutoQuit { get; set; } = true;

        /// <summary>
        /// When set, a successful end of dialog emits
        /// <c>[TEST_RESULT] name=&lt;ResultName&gt; passed=1 failed=0</c> before
        /// quitting, so the global test runner can attribute the result to the
        /// calling test. Left empty by SceneSwitcher, whose smoke runs are
        /// evaluated by log markers instead.
        /// </summary>
        public string ResultName { get; set; }

        /// <summary>
        /// How long the driver tolerates a discovered DialogBox staying idle
        /// (<see cref="IDialogBox.IsDialogEnded"/>) before treating it as an
        /// empty scene and quitting. Covers the gap between scene load and the
        /// first scenario queue.
        /// </summary>
        private const ulong FirstDialogGraceMs = 3000;

        private IDialogBox _dialogBox;
        private float _timer;
        private ulong _dialogFoundAtMs;
        private List<int> _choices = new();
        private int _choiceIndex;
        private string _sceneOnEnd;
        private bool _hasSeenRunningDialog;

        public override void _Ready()
        {
            ParseCmdArgs();
        }

        /// <summary>
        /// Looks the DialogBox up lazily through the tree root instead of the
        /// cached <c>SceneContextNode.FindNode</c>: the driver is often added
        /// before the dialog scene exists, and the context cache would poison
        /// the name as not-found until the next scene-exit event. The cast to
        /// the core IDialogBox contract keeps this file free of mod types.
        /// </summary>
        private IDialogBox FindDialogBox()
        {
            var tree = GetTree();
            if (tree?.Root == null)
                return null;
            return tree.Root.FindChild("DialogBox", true, false) as IDialogBox;
        }

        private void ParseCmdArgs()
        {
            var args = OS.GetCmdlineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith("--choices="))
                {
                    var val = arg.Substring("--choices=".Length);
                    _choices = val.Split(',')
                        .Select(s => int.TryParse(s.Trim(), out var n) ? n : -1)
                        .Where(n => n >= 0)
                        .ToList();
                    break;
                }

                if (arg.StartsWith("--scene2="))
                    _sceneOnEnd = arg.Substring("--scene2=".Length).Trim();
            }
        }

        public override void _Process(double delta)
        {
            if (_dialogBox is not GodotObject liveBox || !GodotObject.IsInstanceValid(liveBox) || liveBox is not Node liveNode || !liveNode.IsInsideTree())
            {
                _dialogBox = FindDialogBox();
                if (_dialogBox == null)
                    return;

                _dialogFoundAtMs = Time.GetTicksMsec();
                _hasSeenRunningDialog = false;

                var choicesInfo = _choices.Count > 0
                    ? "choices=[" + string.Join(",", _choices) + "]"
                    : "no choices, will pick first variant each time";

                GameLogger.Log("TEST", $"DialogAutoDriver started, interval={AdvanceInterval}, {choicesInfo}");
            }

            _timer += (float)delta;
            if (_timer < AdvanceInterval)
                return;
            _timer = 0;

            if (_dialogBox.IsRunning)
                _hasSeenRunningDialog = true;

            // IsDialogEnded is also true while the very first queue has not
            // been loaded yet, so end-handling waits until a dialog was
            // actually observed running. In --scene2 mode the wait is
            // indefinite (the second entry legitimately starts no dialog);
            // otherwise a short grace period lets dialog-less scenes finish.
            var waitingForFirstDialog = !_hasSeenRunningDialog
                && (!string.IsNullOrEmpty(_sceneOnEnd) || Time.GetTicksMsec() - _dialogFoundAtMs < FirstDialogGraceMs);

            if (_dialogBox.IsDialogEnded && !waitingForFirstDialog)
            {
                if (!string.IsNullOrEmpty(_sceneOnEnd))
                {
                    var next = _sceneOnEnd;
                    _sceneOnEnd = null;
                    GameLogger.Log("TEST", $"DialogAutoDriver: dialog ended, switching to '{next}'");
                    SceneContextNode.FindNode<SceneManager>("SceneManager")?.SwitchScene(next);
                    return;
                }

                if (AutoQuit)
                {
                    if (!string.IsNullOrEmpty(ResultName))
                        GameLogger.Log("TEST", $"[TEST_RESULT] name={ResultName} passed=1 failed=0");

                    GameLogger.Log("TEST", "DialogAutoDriver: dialog ended, quitting");
                    GetTree().Quit();
                }
                SetProcess(false);
                return;
            }

            if (_dialogBox.IsWaitingAnswer)
            {
                HandleSelection();
                return;
            }

            _dialogBox.NextAction();
        }

        private void HandleSelection()
        {
            var count = _dialogBox.AnswerVariantCount;
            if (count <= 0)
                return;

            var pick = 0;
            if (_choiceIndex < _choices.Count)
            {
                pick = _choices[_choiceIndex];
                if (pick >= count)
                    pick = 0;
                _choiceIndex++;
            }

            GameLogger.Log("TEST", $"DialogAutoDriver: choice #{_choiceIndex} picking index={pick} out of {count}", LogLevel.Debug);
            _dialogBox.TrySelectVariant(pick);
        }
    }
}
#endif
