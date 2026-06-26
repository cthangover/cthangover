namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Discriminator for pointer-based events, used by IOnRayEvent handlers
    /// to branch on the kind of pointer interaction without subscribing to
    /// multiple interfaces.
    /// </summary>
    public enum PointerEventType
    {
        Down,
        Up,
        Drag,
        Scroll,
    }

}
