using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Kernel seam for the shell's background viewport (the interface engine
    /// mod's ViewBox). SceneManager pushes random scene backgrounds through it
    /// and SaveScreenshotService grabs the current texture for save thumbnails.
    /// The kernel knows this contract, never the implementing mod type — the
    /// same direction as IDialogBox and the LightingBridge delegate slots.
    /// </summary>
    public interface IBackgroundView
    {
        /// <summary>The background TextureRect; may be null until the node is ready.</summary>
        TextureRect Background { get; }

        /// <summary>Replaces the background texture immediately (used mid-scene-transition).</summary>
        void SetBackgroundTexture(Texture2D texture);

        /// <summary>Crossfades to the texture through the black overlay (normal scene entry).</summary>
        void TransitionBackground(Texture2D newTexture);
    }
}
