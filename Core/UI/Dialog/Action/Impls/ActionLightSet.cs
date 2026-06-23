using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Lights;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
	public class ActionLightSet : ActionCommand
	{
		public string LightsJson { get; set; }

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
