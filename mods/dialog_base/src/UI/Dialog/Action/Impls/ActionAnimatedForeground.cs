using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Mods.Dialog.Action;
using Cthangover.Mods.Interface.View;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Frame-animates the scene foreground by swapping ViewBox textures at a
    /// configurable speed: the first frame lasts 1/<see cref="Speed"/> seconds,
    /// every later one 1/<see cref="NextFrameSpeed"/>. Hard cuts, no crossfade.
    /// Frames come from <see cref="Textures"/> or are resolved from
    /// <see cref="SpriteIDs"/> through <see cref="BackgroundFactory"/> (the same
    /// texture names a <c>foreground</c> command takes) on construction
    /// (OnStartQueue).
    /// <para/>
    /// Event-driven: each frame schedules one <see cref="SceneTreeTimer"/> for the
    /// next cut — no per-frame update. The dialog does not wait (NoWait); the
    /// animation keeps playing while the dialog goes on and stops when the queue
    /// ends (OnEndQueue), when another animation starts or when a
    /// <c>foreground</c> command replaces the picture (<see cref="StopActive"/>).
    /// </summary>
    public class ActionAnimatedForeground : ActionCommand
    {
        private static ActionAnimatedForeground active;

        /// <summary>Texture names resolved through <see cref="BackgroundFactory"/>; ignored when <see cref="Textures"/> is set.</summary>
        public List<string> SpriteIDs { get; set; }
        /// <summary>Ready frames used instead of resolving <see cref="SpriteIDs"/> (bypasses the factory like ActionForeground.Texture).</summary>
        public List<Texture2D> Textures { get; set; }
        /// <summary>Frames per second of the first frame.</summary>
        public float Speed { get; set; } = 1f;
        /// <summary>Frames per second of every frame after the first one.</summary>
        public float NextFrameSpeed { get; set; } = 1f;
        /// <summary>Whether playback restarts from the first frame after the last one; otherwise it stops on the last frame.</summary>
        public bool IsLoop { get; set; } = true;

        /// <summary>Whether the animation is still cutting frames.</summary>
        public bool IsAnimating { get; private set; }

        private int currentFrame;
        private List<Texture2D> frames;
        private SceneTreeTimer frameTimer;

        /// <summary>Frames are resolved when the queue starts, so the first cut does not stall on loading.</summary>
        public override ConstructType ConstructType { get; set; } = ConstructType.OnStartQueue;
        /// <summary>The animation lives until the dialog ends, not until the next action.</summary>
        public override DestructType DestructType { get; set; } = DestructType.OnEndQueue;
        /// <summary>The dialog goes on while the foreground animates.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Stops the animation that currently drives the foreground, if any; the shown frame stays.</summary>
        public static void StopActive()
        {
            active?.Stop();
        }

        /// <summary>Resolves the frames.</summary>
        public override void DoConstruct()
        {
            base.DoConstruct();
            LoadFrames();
        }

        /// <summary>Stops the frame timer; the last shown frame stays on the ViewBox.</summary>
        public override void DoDestruct()
        {
            base.DoDestruct();
            Stop();
        }

        private void LoadFrames()
        {
            frames = new List<Texture2D>();
            var source = Textures;
            if (source != null)
            {
                foreach (var tex in source)
                    if (tex != null)
                        frames.Add(tex);
                return;
            }

            if (SpriteIDs == null)
                return;
            foreach (var id in SpriteIDs)
            {
                var tex = BackgroundFactory.Instance.Get(id);
                if (tex != null)
                    frames.Add(tex);
            }
        }

        /// <summary>Stops a previous animation, shows the first frame on the ViewBox and schedules the next cut.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            StopActive();
            currentFrame = 0;
            IsAnimating = frames != null && frames.Count > 0;
            if (!IsAnimating)
                return;

            active = this;
            ShowFrame();
            ScheduleNextFrame(Speed);
        }

        private void ShowFrame()
        {
            var viewBox = Scene.Find<ViewBox>("ViewBox");
            viewBox?.SetForegroundTexture(frames[currentFrame]);
        }

        private void ScheduleNextFrame(float speed)
        {
            if (Godot.Engine.GetMainLoop() is not SceneTree tree)
                return;

            frameTimer = tree.CreateTimer(1f / Mathf.Max(speed, 0.01f));
            frameTimer.Timeout += OnFrameTimeout;
        }

        private void OnFrameTimeout()
        {
            frameTimer = null;
            if (!IsAnimating)
                return;

            currentFrame++;
            if (currentFrame >= frames.Count)
            {
                if (!IsLoop)
                {
                    Stop();
                    return;
                }
                currentFrame = 0;
            }

            ShowFrame();
            ScheduleNextFrame(NextFrameSpeed);
        }

        private void Stop()
        {
            IsAnimating = false;
            if (frameTimer != null)
            {
                frameTimer.Timeout -= OnFrameTimeout;
                frameTimer = null;
            }
            if (active == this)
                active = null;
        }
    }
}
