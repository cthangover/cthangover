using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Lights;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Toggles the light controller's time-of-day mode. When UseTime is false,
    /// static lighting is used; when true, lighting responds to the in-game clock.
    /// </summary>
    public class ActionLightUseTime : ActionCommand
    {
        /// <summary>True = lights respond to in-game time of day; false = static lighting mode.</summary>
        public bool UseTime { get; set; } = true;

        /// <summary>Time mode toggle is instant — the dialog continues without pause.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var controller = SceneContextNode.FindNode<UiLightController>("Lights");
            if (controller != null)
                controller.IsUseLight = UseTime;
        }
    }
}
