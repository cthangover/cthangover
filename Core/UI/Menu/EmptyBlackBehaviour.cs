using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Full-screen black overlay Control. Acts as a blocking backdrop — placed
    /// behind menus to darken the game view and absorb input. No custom logic
    /// beyond what Control provides; scene configuration handles sizing/color.
    /// </summary>
    public partial class EmptyBlackBehaviour : Control
    {
    }
}
