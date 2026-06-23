using Godot;

namespace Cthangover.Core.UI.Event
{
    
    public interface IOnScrollEvent : IEventPriority
    {
        void OnScroll(InputEventMouseButton eventData, EventContext context, PointerEventType pointerEventType);
    }

}
