using Godot;

namespace Cthangover.Core.Utils
{

    /// <summary>
    /// Extension methods for Godot <see cref="Godot.Control"/> nodes that simplify
    /// conversion from a control's layout properties into a <see cref="Godot.Rect2"/>
    /// suitable for intersection tests, viewport culling, and debug drawing.
    /// </summary>
    public static class RectTransformAdditions
    {
        /// <summary>
        /// Constructs a <see cref="Godot.Rect2"/> anchored at the control's
        /// <see cref="Control.GlobalPosition"/> with dimensions from <see cref="Control.Size"/>.
        /// The returned rect uses the global coordinate system, making it directly
        /// usable for mouse-hit testing and overlap checks regardless of the control's
        /// parent hierarchy.
        /// </summary>
        public static Rect2 GetRect(this Control control)
        {
            return new Rect2(control.GlobalPosition, control.Size);
        }

    }

}
