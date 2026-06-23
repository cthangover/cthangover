using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;

namespace Cthangover.Core.UI.Event
{
    
    public interface IOnDialogStartEvent : IEventPriority
    {
        void OnDialogStart(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent);
    }

}
