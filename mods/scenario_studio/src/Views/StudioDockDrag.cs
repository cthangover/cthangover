namespace Cthangover.Mods.ScenarioStudio
{
    using Godot;

    /// <summary>
    /// Mouse drag on one handle of the dock (<c>Frame</c> in
    /// <c>scenes/studio_panel.tscn</c>): the header moves the frame, the edge and
    /// corner grips resize it. The handles and their cursors live in the scene;
    /// this only listens to the handle's <c>GuiInput</c>.
    /// <para/>
    /// Each motion is measured from the press, not from the previous motion, so
    /// a drag that hits a limit (screen border, smallest size) does not leave
    /// the frame lagging behind the mouse once it comes back. The frame keeps
    /// its anchors (right edge, full height): <c>Position</c>/<c>Size</c> rewrite
    /// only the offsets, so a moved dock still follows the right edge when the
    /// window is resized. The smallest size is <see cref="StudioStyles.DockMinSize"/>
    /// or what the dock's content demands, whichever is larger — a narrower
    /// frame would let the panel overflow it.
    /// </summary>
    public sealed class StudioDockDrag
    {
        private readonly Control _frame;
        private readonly Control _dock;
        private readonly Control _handle;
        private readonly StudioDockEdge _edges;
        private bool _dragging;
        private Vector2 _pressedAt;
        private Rect2 _startRect;

        public StudioDockDrag(Control frame, Control dock, Control handle, StudioDockEdge edges)
        {
            _frame = frame;
            _dock = dock;
            _handle = handle;
            _edges = edges;
            handle.GuiInput += OnInput;
        }

        private void OnInput(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse when mouse.Pressed:
                    _dragging = true;
                    _pressedAt = _handle.GetGlobalTransform() * mouse.Position;
                    _startRect = new Rect2(_frame.Position, _frame.Size);
                    _handle.AcceptEvent();
                    break;
                case InputEventMouseButton { ButtonIndex: MouseButton.Left } when _dragging:
                    _dragging = false;
                    _handle.AcceptEvent();
                    break;
                case InputEventMouseMotion motion when _dragging:
                    Apply(_handle.GetGlobalTransform() * motion.Position - _pressedAt);
                    _handle.AcceptEvent();
                    break;
            }
        }

        private void Apply(Vector2 delta)
        {
            var bounds = _frame.GetParentAreaSize();
            var min = StudioStyles.DockMinSize.Max(_dock.GetCombinedMinimumSize());
            var left = _startRect.Position.X;
            var top = _startRect.Position.Y;
            var right = _startRect.End.X;
            var bottom = _startRect.End.Y;

            if (_edges == StudioDockEdge.Move)
            {
                var dx = Mathf.Clamp(delta.X, -left, bounds.X - right);
                var dy = Mathf.Clamp(delta.Y, -top, bounds.Y - bottom);
                left += dx;
                right += dx;
                top += dy;
                bottom += dy;
            }
            else
            {
                if (_edges.HasFlag(StudioDockEdge.Left))
                    left = Mathf.Clamp(left + delta.X, 0, right - min.X);
                if (_edges.HasFlag(StudioDockEdge.Right))
                    right = Mathf.Clamp(right + delta.X, left + min.X, bounds.X);
                if (_edges.HasFlag(StudioDockEdge.Top))
                    top = Mathf.Clamp(top + delta.Y, 0, bottom - min.Y);
                if (_edges.HasFlag(StudioDockEdge.Bottom))
                    bottom = Mathf.Clamp(bottom + delta.Y, top + min.Y, bounds.Y);
            }

            _frame.Position = new Vector2(left, top);
            _frame.Size = new Vector2(right - left, bottom - top);
        }
    }
}
