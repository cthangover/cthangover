using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Lights;
using Cthangover.Core.UI.View;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionBackground : ActionCommand
    {
        public string SpriteID { get; set; }
        public Texture2D Texture { get; set; }
        public Texture2D DepthTexture { get; set; }
        public Texture2D AlbedoTexture { get; set; }

        public override ConstructType ConstructType { get; set; } = ConstructType.OnStartQueue;
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoConstruct()
        {
            base.DoConstruct();
            if (Texture == null && !string.IsNullOrEmpty(SpriteID))
                Texture = BackgroundFactory.Instance.Get(SpriteID);
            if (!string.IsNullOrEmpty(SpriteID))
            {
                DepthTexture = BackgroundFactory.Instance.Get(SpriteID + "_depth");
                AlbedoTexture = BackgroundFactory.Instance.Get(SpriteID + "_albedo");
            }
        }

        public override void DoRun(DialogRuntime runtime)
        {
            if (Texture == null)
                return;

            SceneContextNode.LastBackgroundID = SpriteID;

            var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
            viewBox?.KillTransitionTween();
            viewBox?.SetBackgroundTexture(Texture);

            var controller = UiLightController.Instance;
            controller?.SetupDepthMap(DepthTexture);
            controller?.SetupAlbedoMap(AlbedoTexture);

            Texture = null;
            DepthTexture = null;
            AlbedoTexture = null;
        }
    }
}
