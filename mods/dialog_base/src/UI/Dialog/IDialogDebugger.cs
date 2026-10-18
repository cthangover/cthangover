using Cthangover.Mods.Dialog.Action;

namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Hook a debugging tool installs in <see cref="DialogRuntime.Debugger"/> to
    /// stop a dialog before an action runs (pause, step, breakpoints). The
    /// runtime asks before every action of every dialog — including standalone
    /// runtimes without a <see cref="DialogBox"/> (hover commands) — so the
    /// implementation must answer quickly and hold only dialogs it can resume
    /// (<see cref="DialogBox.Resume"/>).
    /// </summary>
    public interface IDialogDebugger
    {
        /// <summary>
        /// Returns true to hold <paramref name="runtime"/> before
        /// <paramref name="action"/>: the runtime marks itself
        /// <see cref="DialogRuntime.IsHeld"/> and returns without running it;
        /// clicks are ignored until <see cref="DialogBox.Resume"/>.
        /// </summary>
        bool ShouldHold(DialogRuntime runtime, IActionCommand action);
    }
}
