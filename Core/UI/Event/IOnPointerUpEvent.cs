using Godot;

namespace Cthangover.Core.UI.Event
{
    
    public interface IOnPointerUpEvent : IEventPriority
    {
        void OnPointerUp(InputEventMouseButton eventData, EventContext context);
    }

}
