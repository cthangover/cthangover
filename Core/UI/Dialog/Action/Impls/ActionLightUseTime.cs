using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Lights;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionLightUseTime : ActionCommand
    {
        public bool UseTime { get; set; } = true;

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var controller = SceneContextNode.FindNode<UiLightController>("Lights");
            if (controller != null)
                controller.IsUseLight = UseTime;
        }
    }
}
