using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Animation
{
    /// <summary>
    /// Shader-driven frame animation: drives _FrameIndex and _FrameCount uniforms
    /// on the TextureRect's ShaderMaterial rather than swapping textures. This means
    /// all animation frames live in a single GPU texture atlas and the shader selects
    /// the visible frame. Auto-destroys (QueueFree) after totalDuration elapses,
    /// making it suitable for fire-and-forget VFX that don't need external cleanup.
    /// </summary>
    public partial class EffectAnimator : Control, IOnUpdateEvent
    {

        [Export] private TextureRect image;
        [Export] private float totalDuration = 5f;
        [Export] private int frameCount = 5;

        private ShaderMaterial materialInstance;
        private double startTime;
        private float frameInterval;
        private int currentFrame;

        private static readonly StringName FrameIndexId = "_FrameIndex";
        private static readonly StringName FrameCountId = "_FrameCount";

        public override void _Ready()
        {
            frameInterval = totalDuration / frameCount;
            materialInstance = image.Material as ShaderMaterial;
            if (materialInstance != null)
                materialInstance.SetShaderParameter(FrameCountId, frameCount);
        }

        public override void _EnterTree()
        {
        }

        public void Play()
        {
            startTime = Time.GetTicksUsec() / 1_000_000.0;
            currentFrame = 0;
            if (materialInstance != null)
                materialInstance.SetShaderParameter(FrameIndexId, 0f);
        }

        public int Priority => 1;

        public void OnUpdate()
        {
            double now = Time.GetTicksUsec() / 1_000_000.0;
            var elapsed = now - startTime;
            var newFrame = Mathf.Min((int)(elapsed / frameInterval), frameCount - 1);

            if (newFrame != currentFrame && materialInstance != null)
            {
                currentFrame = newFrame;
                materialInstance.SetShaderParameter(FrameIndexId, currentFrame);
            }

            if (elapsed >= totalDuration)
                QueueFree();
        }
    }
}
