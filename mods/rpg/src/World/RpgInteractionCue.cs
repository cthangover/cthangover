namespace Cthangover.Mods.Rpg
{
    using Godot;

    /// <summary>Highlights exactly the event the action key will run. Uses the view's target selection, so hints cannot disagree with input.</summary>
    public partial class RpgInteractionCue : Node2D
    {
        private readonly RpgWorldView view;
        private float elapsed;

        /// <summary>Attaches to one map; no subscriptions or state survive unmount.</summary>
        public RpgInteractionCue(RpgWorldView view)
        {
            this.view = view;
            Name = "InteractionCue";
        }

        /// <summary>Redraws the small corner marker while an action event is in reach.</summary>
        public override void _Process(double delta)
        {
            elapsed += (float)delta;
            QueueRedraw();
        }

        /// <summary>Draws four corners around the target cell, hidden whenever another UI owns input.</summary>
        public override void _Draw()
        {
            if (RpgInput.IsWalkBlocked(this))
                return;
            var target = view.InteractionTarget();
            if (target == null)
                return;
            var rect = new Rect2(target.Position + new Vector2(2, 2), new Vector2(view.TileSize - 4, view.TileSize - 4));
            var color = new Color(1f, 0.83f, 0.48f, 0.65f + 0.2f * Mathf.Sin(elapsed * 3));
            foreach (var corner in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) })
            {
                var dx = corner.X == rect.Position.X ? 5 : -5;
                var dy = corner.Y == rect.Position.Y ? 5 : -5;
                DrawLine(corner, corner + new Vector2(dx, 0), color);
                DrawLine(corner, corner + new Vector2(0, dy), color);
            }
        }
    }
}
