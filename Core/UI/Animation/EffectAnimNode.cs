using Godot;

namespace Cthangover.Core.UI.Animation
{
    /// <summary>
    /// Lightweight, code-only alternative to EffectAnimator that does NOT require
    /// a shader. Constructs an AtlasTexture and shifts its Region rect each frame
    /// to scroll through a horizontal spritesheet. Designed for runtime spawning
    /// via the static SpawnOn factory — it anchors to fill the target Control and
    /// self-destructs when playback finishes, requiring no scene file setup.
    /// </summary>
    public partial class EffectAnimNode : TextureRect
    {
        private AtlasTexture atlas;
        private int frameCount;
        private float frameInterval;
        private float elapsed;
        private int currentFrame = -1;
        private int frameWidth;
        private float totalDuration;

        /// <summary>Factory method that creates the node, attaches it to <paramref name="target"/> filling the full rect, and initializes the spritesheet animation. Returns null if target or sheet is null.</summary>
        /// <param name="target">Parent Control to anchor the animation to.</param>
        /// <param name="sheet">Horizontal spritesheet texture. Frame width = sheet width / frameCount.</param>
        /// <param name="frameCount">Number of frames in the spritesheet.</param>
        /// <param name="totalDuration">Total playback time in seconds; frameInterval = totalDuration / frameCount.</param>
        public static EffectAnimNode SpawnOn(Control target, Texture2D sheet, int frameCount, float totalDuration)
        {
            if (target == null || sheet == null)
                return null;

            var node = new EffectAnimNode();
            target.AddChild(node);
            node.AnchorLeft = 0;
            node.AnchorTop = 0;
            node.AnchorRight = 1;
            node.AnchorBottom = 1;
            node.OffsetLeft = 0;
            node.OffsetTop = 0;
            node.OffsetRight = 0;
            node.OffsetBottom = 0;
            node.MouseFilter = MouseFilterEnum.Ignore;
            node.ZIndex = 100;
            node.Init(sheet, frameCount, totalDuration);
            return node;
        }

        private void Init(Texture2D sheet, int frameCount, float totalDuration)
        {
            this.frameCount = frameCount;
            this.totalDuration = totalDuration;
            frameInterval = totalDuration / frameCount;
            frameWidth = sheet.GetWidth() / frameCount;

            atlas = new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2(0, 0, frameWidth, sheet.GetHeight()),
            };
            Texture = atlas;
            ExpandMode = ExpandModeEnum.IgnoreSize;
            StretchMode = StretchModeEnum.KeepAspectCentered;
            SetFrame(0);
        }

        public override void _Process(double delta)
        {
            elapsed += (float)delta;
            var frame = Mathf.Min((int)(elapsed / frameInterval), frameCount - 1);
            if (frame != currentFrame)
                SetFrame(frame);

            if (elapsed >= totalDuration)
                QueueFree();
        }

        private void SetFrame(int frame)
        {
            currentFrame = frame;
            atlas.Region = new Rect2(frame * frameWidth, 0, frameWidth, atlas.Region.Size.Y);
        }
    }
}
