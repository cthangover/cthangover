#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    public partial class DialogAutoDriver : Node
    {
        [Export] public float AdvanceInterval { get; set; } = 0.1f;
        [Export] public bool AutoQuit { get; set; } = true;

        private DialogBox _dialogBox;
        private float _timer;
        private List<int> _choices = new();
        private int _choiceIndex;
        private string _sceneOnEnd;
        private bool _hasSeenRunningDialog;

        public override void _Ready()
        {
            ParseCmdArgs();

            _dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");
            if (_dialogBox == null)
            {
                GameLogger.Log("TEST", "DialogAutoDriver: DialogBox not found, disabling", LogLevel.Error);
                SetProcess(false);
                return;
            }

            var choicesInfo = _choices.Count > 0
                ? "choices=[" + string.Join(",", _choices) + "]"
                : "no choices, will pick first variant each time";

            GameLogger.Log("TEST", $"DialogAutoDriver started, interval={AdvanceInterval}, {choicesInfo}");
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
            if (_dialogBox == null || !_dialogBox.IsInsideTree())
                return;

            _timer += (float)delta;
            if (_timer < AdvanceInterval)
                return;
            _timer = 0;

            if (_dialogBox.IsRunning)
                _hasSeenRunningDialog = true;

            // Runtime.IsEnd is also true while the very first queue has not
            // been loaded yet, so end-handling waits until a dialog was
            // actually observed running (relevant for --scene2 revisits where
            // the second entry legitimately starts no dialog at all).
            var waitingForFirstDialog = !string.IsNullOrEmpty(_sceneOnEnd) && !_hasSeenRunningDialog;

            if (_dialogBox.Runtime.IsEnd && !waitingForFirstDialog)
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

                    GameLogger.Log("TEST", "DialogAutoDriver: dialog ended, quitting");
                    GetTree().Quit();
                }
                SetProcess(false);
                return;
            }

            if (_dialogBox.Runtime.IsWaitAnswer)
            {
                HandleSelection();
                return;
            }

            _dialogBox.NextAction();
        }

        private void HandleSelection()
        {
            var answerBox = _dialogBox.FindChild("AnswerBox", true, false) as AnswerBox;
            if (answerBox == null)
                return;

            var variants = answerBox.CreateModels() as ICollection<SelectVariant>;
            if (variants == null || variants.Count == 0)
                return;

            var list = new List<SelectVariant>(variants);
            var pick = 0;

            if (_choiceIndex < _choices.Count)
            {
                pick = _choices[_choiceIndex];
                if (pick >= list.Count)
                    pick = 0;
                _choiceIndex++;
            }
            
            GameLogger.Log("TEST", $"DialogAutoDriver: choice #{_choiceIndex} picking index={pick} out of {list.Count}", LogLevel.Debug);
            _dialogBox.SelectVariant(list[pick]);
        }
    }
}
#endif
