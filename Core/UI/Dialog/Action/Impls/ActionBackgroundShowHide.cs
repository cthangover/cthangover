using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionBackgroundShowHide : ActionCommand
    {
        public BackgroundActionType ActionType { get; set; } = BackgroundActionType.Show;
        public float Duration { get; set; } = 1f;

        private Tween activeTween;

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            var targetAlpha = ActionType == BackgroundActionType.Show ? 1f : 0f;

            if (dialogBox.GetParent() is Control backgroundNode)
            {
                StopTween();
                activeTween = backgroundNode.CreateTween();
                activeTween.TweenProperty(backgroundNode, "modulate:a", targetAlpha, Duration);
            }
        }

        public override void DoDestruct()
        {
            base.DoDestruct();
            StopTween();
        }

        private void StopTween()
        {
            if (activeTween != null)
            {
                activeTween.Kill();
                activeTween = null;
            }
        }
    }
}
