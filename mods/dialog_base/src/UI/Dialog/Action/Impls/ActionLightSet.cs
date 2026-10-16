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
    /// Applies static lights from a JSON string to the UiLightController.
    /// An empty or null string clears all static lights. The JSON is deserialized
    /// into LightDef objects with viewport-relative coordinates, which are then
    /// converted to pixel positions. Supports up to 10 static lights plus the
    /// player's dynamic lamp.
    /// </summary>
	public class ActionLightSet : ActionCommand
	{
        /// <summary>JSON string defining static light positions and properties. Empty or null clears all static lights.</summary>
        public string LightsJson { get; set; }

        /// <summary>Lights are set imperatively — the dialog continues immediately.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

		/// <summary>Clears static lights when <see cref="LightsJson"/> is empty, otherwise parses and applies the light definitions to the scene's <see cref="UiLightController"/>.</summary>
		public override void DoRun(DialogRuntime runtime)
		{
			var controller = Scene.Find<UiLightController>("Lights");
			if (controller == null)
				return;

			if (string.IsNullOrEmpty(LightsJson))
				controller.ClearStaticLights();
			else
				controller.SetStaticLights(LightsJson);
		}
	}
}
