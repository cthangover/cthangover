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

        public event Action NextFrame;
        public event Action NextCycle;

        [Export] private Color clearColor = new(1f, 1f, 1f, 0f);
        [Export] private Color completeColor = new(1f, 1f, 1f, 1f);
        
        private int currentFrame;
        private double timestamp;
        private float percent;
        private bool wait;
        private float waitPercent;
        private bool isStarted;

        public bool IsLoop { get => isLoop; set => isLoop = value; }
        public float Speed { get => speed; set => speed = value; }
        public float NextFrameSpeed { get => nextFrameSpeed; set => nextFrameSpeed = value; }
        public bool IsStarted => isStarted;
        
        public List<Texture2D> FrameSet { get; set; }

        public void Clear()
        {
            isStarted = false;
            FrameSet = null;
            current.Texture = empty;
            current.Modulate = Colors.Transparent;
            next.Texture = empty;
            next.Modulate = Colors.Transparent;
        }

        public void Pause()
        {
            isStarted = false;
        }

        public void Play()
        {
            currentFrame = 0;
            isStarted = true;
            timestamp = Time.GetTicksUsec() / 1_000_000.0;
            
            current.Texture = empty;
            next.Modulate = clearColor;
            next.Texture = FrameSet[currentFrame];
        }
        
        public int Priority => 1;
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
