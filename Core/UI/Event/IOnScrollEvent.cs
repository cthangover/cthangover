using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires on mouse scroll with the pointer event type, so handlers can
    /// distinguish scroll on different input contexts.
    /// </summary>
    public interface IOnScrollEvent : IEventPriority
    {
        void OnScroll(InputEventMouseButton eventData, EventContext context, PointerEventType pointerEventType);
    }

}
