using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Notified when a dialog queue begins executing. Receives the dialog, runtime,
    /// and triggering ExecutableEvent — enables pre-dialog setup (e.g. hiding
    /// conflicting UI before dialog text appears).
    /// </summary>
    public interface IOnDialogStartEvent : IEventPriority
    {
        void OnDialogStart(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent);
    }

}
