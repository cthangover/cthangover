namespace Cthangover.Core.UI.Dialog.Action
{
    /// <summary>
    /// Determines how the DialogRuntime advances past this action. NoWait chains
    /// immediately to the next action (for imperative commands). WaitClick pauses
    /// until user input (click/key). WaitSelect pauses for variant selection.
    /// WaitTime pauses for a fixed duration. WaitEvent pauses until an external
    /// event marks the action as destructed.
    /// </summary>
    public enum WaitType
    {
        NoWait,
        WaitClick,
        WaitSelect,
        WaitTime,
        WaitEvent,
    }
    
}
