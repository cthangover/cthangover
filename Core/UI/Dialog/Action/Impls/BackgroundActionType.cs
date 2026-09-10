namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Discriminator for background show/hide transitions.
    /// </summary>
    public enum BackgroundActionType
    {
        /// <summary>Fade the dialog background visible (target alpha = 1.0).</summary>
        Show,
        /// <summary>Fade the dialog background transparent (target alpha = 0.0).</summary>
        Hide
    }
}
