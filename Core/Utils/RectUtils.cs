using Godot;

namespace Cthangover.Core.Utils
{

    public static class RectTransformAdditions
    {

        public static Rect2 GetRect(this Control control)
        {
            return new Rect2(control.GlobalPosition, control.Size);
        }

    }

}
