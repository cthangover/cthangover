namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// iOS-style touch lifecycle phases. Used by DeviceInput to derive phase
    /// from mouse button state between frames.
    /// </summary>
    public enum TouchPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled
    }
}
