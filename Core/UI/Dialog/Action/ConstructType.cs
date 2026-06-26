namespace Cthangover.Core.UI.Dialog.Action
{
    /// <summary>
    /// Controls when an action's Construct() fires. OnStartQueue — construct
    /// when the dialog queue is first loaded (good for preloading); OnStartAction —
    /// lazy-construct just before the action runs (saves resources for unreached
    /// branches). Default for most actions is OnStartAction.
    /// </summary>
    public enum ConstructType
    {
        OnStartQueue,
        OnStartAction
    }
    
}
