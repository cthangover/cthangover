namespace Cthangover.Core.UI.Dialog.Action
{
    /// <summary>
    /// Controls when an action's Destruct() fires. OnEndAction — immediately after
    /// the action advances (typical for most commands). OnDelayed — deferred until
    /// specifically cleaned up (for visual state that must persist across actions).
    /// OnEndQueue — destruct when the entire dialog queue finishes (for resources
    /// that must survive the whole dialog but not beyond).
    /// </summary>
    public enum DestructType
    {
        /// <summary>Destruct immediately when the action is advanced past. Default for most commands.</summary>
        OnEndAction,
        /// <summary>Destruct is deferred — must be manually triggered or triggered via <see cref="ActionCommand.DelayedDestruct"/>.</summary>
        OnDelayed,
        /// <summary>Destruct when the entire dialog queue finishes. Use for resources that must survive the whole dialog but not beyond.</summary>
        OnEndQueue,
    }

}
