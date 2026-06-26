using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Notified when a dialog queue finishes executing. Receives the dialog, its
    /// runtime state, and the ExecutableEvent that triggered it, so handlers can
    /// inspect dialog variables or chain further actions.
    /// </summary>
    public interface IOnDialogEndEvent : IEventPriority
    {
        void OnDialogEnd(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent);
    }

}
