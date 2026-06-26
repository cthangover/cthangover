using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Sets the background tint color. DoRun is currently a no-op — the Color
    /// property is stored for external systems to read. Likely intended as a
    /// declarative instruction consumed by a background-rendering node rather
    /// than applied imperatively.
    /// </summary>
    public class ActionBackgroundColor : ActionCommand
    {
        public Color Color { get; set; } = Colors.Black;

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
        }
    }
}
