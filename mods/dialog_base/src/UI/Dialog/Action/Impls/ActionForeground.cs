using Cthangover.Core.Factories.Impls;
using Cthangover.Mods.Interface.View;
using Godot;
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

        /// <summary>Resolves <see cref="SpriteID"/> through the background factory unless a direct texture override is set.</summary>
        public override void DoConstruct()
        {
            base.DoConstruct();
            if (Texture == null && !string.IsNullOrEmpty(SpriteID))
                Texture = BackgroundFactory.Instance.Get(SpriteID);
        }

        /// <summary>Applies the preloaded texture to the ViewBox foreground, then nulls it so a reused action cannot re-apply stale data.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var viewBox = Scene.Find<ViewBox>("ViewBox");
            viewBox?.SetForegroundTexture(Texture);
            Texture = null;
        }
    }
}
