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
        /// <summary>Don't wait — the runtime advances to the next action immediately in the same Run call.</summary>
        NoWait,
        /// <summary>Pause until the player clicks or presses the accept key. Used by dialog text actions.</summary>
        WaitClick,
        /// <summary>Pause until the player selects a choice variant. Used by choice menus.</summary>
        WaitSelect,
        /// <summary>Pause for a fixed duration in seconds (<see cref="ActionCommand.WaitTime"/>).</summary>
        WaitTime,
        /// <summary>Pause until an external event marks the action as destructed (<see cref="ActionCommand.IsDestructed"/>).</summary>
        WaitEvent,
    }
    
}
