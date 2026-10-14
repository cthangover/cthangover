using Cthangover.Mods.Dialog.Action;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Tween-driven background fade in/out. Animates the modulate alpha of the
    /// dialog box's parent Control over Duration seconds. Creates and manages
    /// its own Tween internally, killing any previous tween before starting a
    /// new one to prevent overlap. Can optionally set WaitType to WaitTime with
    /// a matching duration so the dialog pauses during the transition.
    /// </summary>
    public class ActionBackgroundShowHide : ActionCommand
    {
        /// <summary>Whether to fade the background in (Show) or out (Hide).</summary>
        public BackgroundActionType ActionType { get; set; } = BackgroundActionType.Show;
        /// <summary>Duration of the fade tween in seconds.</summary>
        public float Duration { get; set; } = 1f;

        private Tween activeTween;

        /// <summary>Default NoWait — overridden by the DialogQueue builder if the wait flag is set, changing to WaitTime to pause during transition.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Starts an alpha tween of the dialog box parent's <c>modulate:a</c> towards fully visible (Show) or fully transparent (Hide).</summary>
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

        /// <summary>Kills the fade tween so a destructed action cannot keep animating.</summary>
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
