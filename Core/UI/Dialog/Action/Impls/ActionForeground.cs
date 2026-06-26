using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.View;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Sets the scene foreground texture on the ViewBox. Resolves SpriteID through
    /// BackgroundFactory on construction (OnStartQueue) so the texture is loaded
    /// before the dialog starts. Sets the texture to null after applying it —
    /// ActionCommand instances may be reused by the runtime, and nulling prevents
    /// double-applying stale references.
    /// </summary>
    public class ActionForeground : ActionCommand
    {
        public string SpriteID { get; set; }
        public Texture2D Texture { get; set; }

        public override ConstructType ConstructType { get; set; } = ConstructType.OnStartQueue;
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoConstruct()
        {
            base.DoConstruct();
            if (Texture == null && !string.IsNullOrEmpty(SpriteID))
                Texture = BackgroundFactory.Instance.Get(SpriteID);
        }

        public override void DoRun(DialogRuntime runtime)
        {
            var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
            viewBox?.SetForegroundTexture(Texture);
            Texture = null;
        }
    }
}
