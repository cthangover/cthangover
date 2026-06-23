using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Animation
{
    
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
