using Godot;

namespace Cthangover.Core.UI.Event
{
    
    public interface IOnPointerClickEvent : IEventPriority
    {
        void OnPointerClick(InputEventMouseButton eventData, EventContext context, Godot.Collections.Array hits, int count, float pressingTime);
    }

}
