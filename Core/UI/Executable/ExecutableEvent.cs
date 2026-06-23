using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Executable
{

    public partial class ExecutableEvent : Node, IOnUpdateEvent
    {
        protected DialogBox dialogBox;
        private SceneEventController eventController;
        private float timestamp;

        public bool IsFirstRun { get; private set; } = true;

        [Export] public bool IsOneRun { get; set; } = false;

        [Export] public bool CanRepeat { get; set; } = true;

        public virtual bool CheckConditions => true;
        public virtual string ID => GetType().FullName;

        public int Priority => 1;

        public override void _Ready()
        {
            if (dialogBox == null)
            {
                var root = GetTree()?.Root;
                if (root != null)
                {
                    dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");
                }
            }

            var isInChain = GetParent() is IExecutableEventChain;
            if (!isInChain)
            {
                eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
                eventController?.AddUpdateEventListener(this);
            }

            GameLogger.Log("EVENT", $"{GetType().Name}._Ready: dialogBox={(dialogBox != null ? "found" : "null")}, chain={isInChain}, registered={!isInChain}", LogLevel.Debug);
        }

        public override void _ExitTree()
        {
            if (eventController != null)
                eventController.RemoveUpdateEventListener(this);
        }

        public void OnUpdate()
        {
            if (!IsFirstRun && !CanRepeat)
                return;

            var time = Time.GetTicksUsec() / 1_000_000.0;
            if (time - timestamp < 0.5f)
                return;
            timestamp = (float)time;

            if (dialogBox == null || dialogBox.IsRunning)
                return;

            RunDialog();
        }

        public bool RunDialog()
        {
            if (!IsFirstRun && IsOneRun)
                return false;

            if (dialogBox == null)
                return false;

            if (dialogBox.Locker != null)
                return false;

            GameLogger.Log("EVENT", $"{GetType().Name}.RunDialog: starting dialog...");

            var dlg = new DialogQueue();
            CreateDialog(dlg);

            dialogBox.SetDialogQueueAndRun(dlg, null, 0, this);
            IsFirstRun = false;
            return true;
        }

        protected virtual void CreateDialog(DialogQueue dlg)
        {
        }
    }

}
