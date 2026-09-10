namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Per-frame update callback. Subscribers receive <see cref="OnUpdate"/>
    /// on every frame when the <see cref="SceneEventController"/> is active.
    /// </summary>
    public interface IOnUpdateEvent : IEventPriority
    {
        void OnUpdate();
    }
}
