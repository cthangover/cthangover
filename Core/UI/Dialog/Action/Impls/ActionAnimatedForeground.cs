using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.View;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Frame-animates the scene foreground by swapping ViewBox textures at a
    /// configurable speed. Loads frames from SpriteIDs on construction
    /// (OnStartQueue). Unlike AnimationController which crossfades, this does
    /// hard cuts — simpler but acceptable for foreground effects. The speed
    /// switches to NextFrameSpeed after the first frame so first-frame delay
    /// can differ from subsequent frames. Registers as IOnUpdateEvent for
    /// per-frame advancement. Non-looping mode auto-stops at last frame.
    /// </summary>
    public class ActionAnimatedForeground : ActionCommand, IOnUpdateEvent
    {
        /// <summary>Update priority. Runs after higher-priority listeners. Value 0.</summary>
        public int Priority => 0;

        /// <summary>List of sprite resource paths to display as animation frames. Loaded in <see cref="DoConstruct"/>.</summary>
        public List<string> SpriteIDs { get; set; }
        /// <summary>Initial frame rate multiplier for the first frame.</summary>
        public float Speed { get; set; } = 1f;
        /// <summary>Frame rate multiplier for subsequent frames after the first. Allows different pacing for the initial frame.</summary>
        public float NextFrameSpeed { get; set; } = 1f;
        /// <summary>Whether the animation wraps after the last frame. When false, playback stops at the final frame.</summary>
        public bool IsLoop { get; set; } = true;

        private int currentFrame;
        private float elapsed;
        private bool isAnimating;
        private List<Texture2D> frames;

        /// <summary>Preloads frames when the dialog queue is first loaded, so textures are ready before playback.</summary>
        public override ConstructType ConstructType { get; set; } = ConstructType.OnStartQueue;
        /// <summary>Animation runs in the background — the dialog queue continues immediately.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoConstruct()
        {
            base.DoConstruct();
            LoadFrames();
        }

        public override void DoDestruct()
        {
            base.DoDestruct();
            isAnimating = false;
        }

        private void LoadFrames()
        {
            frames = new List<Texture2D>();
            if (SpriteIDs == null)
                return;
            foreach (var id in SpriteIDs)
            {
                var tex = GD.Load<Texture2D>(id);
                if (tex != null)
                    frames.Add(tex);
            }
        }

        public override void DoRun(DialogRuntime runtime)
        {
            currentFrame = 0;
            elapsed = 0f;
            isAnimating = frames != null && frames.Count > 0;

            if (isAnimating)
            {
                var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
                viewBox?.SetForegroundTexture(frames[0]);
            }
        }

        void IOnUpdateEvent.OnUpdate()
        {
            if (!isAnimating || frames == null || frames.Count == 0)
                return;

            var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
            if (viewBox == null)
                return;

            elapsed += 1f / 60f;
            var frameDuration = 1f / Mathf.Max(Speed, 0.01f);
            if (elapsed >= frameDuration)
            {
                elapsed -= frameDuration;
                currentFrame++;
                if (currentFrame >= frames.Count)
                {
                    if (IsLoop)
                        currentFrame = 0;
                    else
                    {
                        isAnimating = false;
                        return;
                    }
                }
                viewBox.SetForegroundTexture(frames[currentFrame]);
                Speed = NextFrameSpeed;
            }
        }
    }
}
