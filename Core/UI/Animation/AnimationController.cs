using System;
using System.Collections.Generic;
using Cthangover.Core.UI.Event;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Animation
{
    /// <summary>
    /// Frame-by-frame sprite animation via crossfade between two TextureRect nodes.
    /// Rather than using a spritesheet, it keeps a "current" and "next" texture and
    /// lerps their modulate between transparent and opaque, creating smooth transitions.
    /// A built-in wait/waitPercent mechanism holds the "next" frame fully visible for
    /// a configurable interval before advancing, producing a paced flip-book effect.
    /// Exposes NextFrame/NextCycle events so external systems can sequence or react
    /// to animation milestones without polling.
    /// </summary>
    public partial class AnimationController : Control, IOnUpdateEvent
    {

        [Export] private TextureRect current;
        [Export] private TextureRect next;
        [Export] private Texture2D empty;

        [Export] private bool isLoop = true;
        [Export] private float speed = 1f;
        [Export] private float nextFrameSpeed = 0.2f;

        /// <summary>Fires when the current frame transition completes and the next frame texture is assigned.</summary>
        public event Action NextFrame;
        /// <summary>Fires when the animation wraps around (looping) or reaches the final frame (non-looping).</summary>
        public event Action NextCycle;

        [Export] private Color clearColor = new(1f, 1f, 1f, 0f);
        [Export] private Color completeColor = new(1f, 1f, 1f, 1f);
        
        private int currentFrame;
        private double timestamp;
        private float percent;
        private bool wait;
        private float waitPercent;
        private bool isStarted;

        /// <summary>When true, the animation wraps from the last frame back to the first. When false, playback stops at the final frame.</summary>
        public bool IsLoop { get => isLoop; set => isLoop = value; }
        /// <summary>Crossfade speed multiplier between frames. Higher values produce faster transitions.</summary>
        public float Speed { get => speed; set => speed = value; }
        /// <summary>Hold duration multiplier for the "wait" phase after a frame transition completes.</summary>
        public float NextFrameSpeed { get => nextFrameSpeed; set => nextFrameSpeed = value; }
        /// <summary>Whether playback is currently active. False on startup and after <see cref="Pause"/> or <see cref="Clear"/>.</summary>
        public bool IsStarted => isStarted;
        
        /// <summary>Ordered list of textures to cycle through. Set externally before calling <see cref="Play"/>.</summary>
        public List<Texture2D> FrameSet { get; set; }

        /// <summary>Resets to idle state: stops playback, clears frame set, and blanks both texture nodes to transparent.</summary>
        public void Clear()
        {
            isStarted = false;
            FrameSet = null;
            current.Texture = empty;
            current.Modulate = Colors.Transparent;
            next.Texture = empty;
            next.Modulate = Colors.Transparent;
        }

        /// <summary>Stops playback without clearing textures, freezing on the current frame.</summary>
        public void Pause()
        {
            isStarted = false;
        }

        /// <summary>Starts or restarts playback from the first frame in <see cref="FrameSet"/>.</summary>
        public void Play()
        {
            currentFrame = 0;
            isStarted = true;
            timestamp = Time.GetTicksUsec() / 1_000_000.0;
            
            current.Texture = empty;
            next.Modulate = clearColor;
            next.Texture = FrameSet[currentFrame];
        }
        
        /// <summary>Update priority. Lower numbers run earlier in the frame. Default 1.</summary>
        public int Priority => 1;
        /// <summary>Advances the crossfade each frame. Handles both the transition phase (lerping modulate) and the wait phase (holding the next frame visible).</summary>
        public void OnUpdate()
        {
            if(!isStarted)
                return;
            
            if (Lists.IsEmpty(FrameSet))
            {
                Clear();
                return;
            }

            double now = Time.GetTicksUsec() / 1_000_000.0;
            if (!wait)
            {
                float delta = (float)(now - timestamp) * speed;
                percent = Mathf.Min(percent + delta, 1f);
                current.Modulate = completeColor.Lerp(Colors.Transparent, percent);
                next.Modulate = Colors.Transparent.Lerp(completeColor, percent);
            }
            else
            {
                float delta = (float)(now - timestamp) * nextFrameSpeed;
                waitPercent = Mathf.Min(waitPercent + delta, 1f);
            }
            
            timestamp = now;

            if (percent >= 1f && !wait)
            {
                wait = true;
                SwitchNextFrame();
                percent = 0f;
            }
            if (waitPercent >= 1f && wait)
            {
                wait = false;
                waitPercent = 0f;
            }
        }

        private void SwitchNextFrame()
        {
            NextFrame?.Invoke();
            if (Lists.IsEmpty(FrameSet))
            {
                Clear();
                return;
            }

            var needNextCycle = false;
            currentFrame++;
            if (FrameSet.Count <= currentFrame)
            {
                if (isLoop)
                    currentFrame = 0;
                else
                {
                    currentFrame--;
                    Pause();
                }
                needNextCycle = true;
            }
            
            current.Modulate = completeColor;
            current.Texture = next.Texture;
            
            next.Modulate = clearColor;
            next.Texture = FrameSet[currentFrame];
            
            if(needNextCycle)
                NextCycle?.Invoke();
        }
    }
}
