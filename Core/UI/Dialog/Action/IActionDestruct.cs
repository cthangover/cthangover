namespace Cthangover.Core.UI.Dialog.Action
{
    /// <summary>
    /// Minimal destructible contract — anything in the dialog runtime's object list
    /// that must be cleaned up when a dialog ends.
    /// </summary>
    public interface IActionDestruct
    {
        void Destruct();
    }
    
}
