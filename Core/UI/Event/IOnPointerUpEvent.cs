using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires when a pointer button is released. Paired with IOnPointerDownEvent
    /// for stateful press-tracking in custom controls.
    /// </summary>
    public interface IOnPointerUpEvent : IEventPriority
    {
        void OnPointerUp(InputEventMouseButton eventData, EventContext context);
    }

}
