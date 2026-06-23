using Godot;

namespace Cthangover.Core.UI.View
{

    public partial class ZoomViewBoxController : Node
    {

        [Export] private ViewBox viewBox;

        public void OnChangeZoom(float value)
        {
            if (viewBox == null)
                return;

            viewBox.SetZoom(value);
        }

    }

}
