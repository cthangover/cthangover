using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Core widget contract: every visible UI piece exposes Show/Hide, a Body node
    /// (the actual rendered subtree), a Rect (the layout Control), and a Visible flag.
    /// The separation of Body from Rect allows the widget's root Control to handle
    /// layout while a different child renders the content.
    /// </summary>
    public interface IWidget
    {
        bool Visible { get; set; }
        Control Rect { get; }
        Node Body { get; }
        
        void Show();

        void Hide();
    }
}
