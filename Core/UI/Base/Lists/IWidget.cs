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
        /// <summary>Whether the widget is currently visible. Setting this routes through <see cref="Show"/> and <see cref="Hide"/> respectively.</summary>
        bool Visible { get; set; }
        /// <summary>The root Control used for layout and positioning of this widget within its parent.</summary>
        Control Rect { get; }
        /// <summary>The subtree that actually renders content. May differ from <see cref="Rect"/> when layout and rendering are split across nodes.</summary>
        Node Body { get; }
        
        /// <summary>Makes the widget visible and triggers the construct lifecycle if this is the first display.</summary>
        void Show();

        /// <summary>Hides the widget and triggers destruct lifecycle to clean up state.</summary>
        void Hide();
    }
}
