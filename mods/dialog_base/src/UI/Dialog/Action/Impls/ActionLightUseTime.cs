using Cthangover.Mods.Dialog.Action;
using Cthangover.Mods.Time;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
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

        /// <summary>Switches the scene's <see cref="UiLightController"/> between time-of-day lighting and static lights.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var controller = Scene.Find<UiLightController>("Lights");
            if (controller != null)
                controller.IsUseLight = UseTime;
        }
    }
}
