namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires every frame from SceneEventController._Process. Subscribers should
    /// be lightweight — this runs for all active listeners each frame.
    /// </summary>
    public interface IOnUpdateEvent : IEventPriority
    {
        void OnUpdate();
    }

}
