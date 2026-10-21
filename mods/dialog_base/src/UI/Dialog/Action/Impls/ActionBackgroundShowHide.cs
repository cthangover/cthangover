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
    /// dialog box's parent Control over Duration seconds. The fade belongs to the
    /// background, not to the action: by default (NoWait) the runtime destructs the
    /// action right after it runs, and the fade must go on. So the tween is shared
    /// by all instances — a new fade kills the previous one to prevent overlap —
    /// and destructing the action leaves it running; the tween is bound to the
    /// background node and dies with it. Can optionally set WaitType to WaitTime
    /// with a matching duration so the dialog pauses during the transition.
    /// </summary>
    public class ActionBackgroundShowHide : ActionCommand
    {
        /// <summary>Whether to fade the background in (Show) or out (Hide).</summary>
        public BackgroundActionType ActionType { get; set; } = BackgroundActionType.Show;
        /// <summary>Duration of the fade tween in seconds.</summary>
        public float Duration { get; set; } = 1f;

        private static Tween activeTween;

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

        private static void StopTween()
        {
            if (activeTween != null)
            {
                if (GodotObject.IsInstanceValid(activeTween))
                    activeTween.Kill();
                activeTween = null;
            }
        }
    }
}
