using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Lights;

namespace Cthangover.Core.UI.Dialog.Action.Impls
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

		public override void DoRun(DialogRuntime runtime)
		{
			var controller = SceneContextNode.FindNode<UiLightController>("Lights");
			if (controller == null)
				return;

			if (string.IsNullOrEmpty(LightsJson))
				controller.ClearStaticLights();
			else
				controller.SetStaticLights(LightsJson);
		}
	}
}
