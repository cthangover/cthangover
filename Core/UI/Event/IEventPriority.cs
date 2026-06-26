namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Priority marker for event subscriber sorting. Lower values execute first.
    /// Used by SceneEventController to sort listener lists so high-priority handlers
    /// (e.g. input blocking) run before general-purpose subscribers.
    /// </summary>
    public interface IEventPriority
    {
        int Priority { get; }
    }

}
