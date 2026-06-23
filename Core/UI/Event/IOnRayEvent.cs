using Godot;

namespace Cthangover.Core.UI.Event
{

    public interface IOnRayEvent : IEventPriority
    {
        void OnRayClick(InputEventMouseButton eventData, EventContext context, PointerEventType pointerEventType, Godot.Collections.Array hits, int count);
    }

}
