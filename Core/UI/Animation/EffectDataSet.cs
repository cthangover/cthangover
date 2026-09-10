using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Animation
{
    /// <summary>
    /// Editor-configured bridge between a frame Texture2D array and an AnimationController.
    /// Converts Godot's typed Array into a List, wires the NextCycle event, and starts
    /// playback. When the animation completes one cycle, it detaches the handler and
    /// self-destructs — the "fire and forget" glue between artist-authored data and
    /// the animation runtime.
    /// </summary>
    public partial class EffectDataSet : Control
    {
        
        [Export] private AnimationController controller;
        [Export] private Godot.Collections.Array<Texture2D> frameSet;

        public override void _Ready()
        {
            AnchorLeft = 0f;
            AnchorRight = 1f;
            AnchorTop = 0f;
            AnchorBottom = 1f;

            var frames = new List<Texture2D>();
            foreach (var tex in frameSet)
                frames.Add(tex);

            controller.Clear();
            controller.NextCycle += ControllerOnNextCycle;
            controller.FrameSet = frames;
            controller.Play();
        }

        private void ControllerOnNextCycle()
        {
            controller.NextCycle -= ControllerOnNextCycle;
            controller.Clear();
            QueueFree();
        }
    }
}
