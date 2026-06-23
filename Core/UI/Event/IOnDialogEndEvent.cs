using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;

namespace Cthangover.Core.UI.Event
{
    
    public interface IOnDialogEndEvent : IEventPriority
    {
        void OnDialogEnd(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent);
    }

}
