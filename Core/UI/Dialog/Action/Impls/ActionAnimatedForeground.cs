using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.View;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionAnimatedForeground : ActionCommand, IOnUpdateEvent
    {
        public int Priority => 0;

        public List<string> SpriteIDs { get; set; }
        public float Speed { get; set; } = 1f;
        public float NextFrameSpeed { get; set; } = 1f;
        public bool IsLoop { get; set; } = true;

        private int currentFrame;
        private float elapsed;
        private bool isAnimating;
        private List<Texture2D> frames;

        public override ConstructType ConstructType { get; set; } = ConstructType.OnStartQueue;
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
