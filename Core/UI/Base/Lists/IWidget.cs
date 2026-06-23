using Godot;

namespace Cthangover.Core.UI
{
    public interface IWidget
    {
        bool Visible { get; set; }
        Control Rect { get; }
        Node Body { get; }
        
        void Show();

        void Hide();
    }
}
