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
        OnEndAction,
        OnDelayed,
        OnEndQueue,
    }

}
