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
        /// <summary>Construct when the dialog queue is first loaded. Useful for actions that need to preload resources.</summary>
        OnStartQueue,
        /// <summary>Construct lazily just before the action runs. Default for most actions — saves resources for unreached branches.</summary>
        OnStartAction
    }
    
}
