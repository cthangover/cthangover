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
        /// <summary>Sprite resource ID resolved through <see cref="BackgroundFactory"/> on construction.</summary>
        public string SpriteID { get; set; }
        /// <summary>Direct texture override. Bypasses factory resolution. Nulled after application to prevent stale reuse across dialog cycles.</summary>
        public Texture2D Texture { get; set; }

        /// <summary>Preloads the texture when the dialog queue is first created.</summary>
        public override ConstructType ConstructType { get; set; } = ConstructType.OnStartQueue;
        /// <summary>Foreground is set imperatively — the dialog continues immediately.</summary>
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
