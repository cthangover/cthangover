using Godot;

namespace Cthangover.Core.UI.Event
{
    
    public interface IOnPointerDownEvent : IEventPriority
    {
        void OnPointerDown(InputEventMouseButton eventData, EventContext context);
    }

}
