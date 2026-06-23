using Godot;

namespace Cthangover.Core.UI.Event
{
    
    public interface IOnUIClickEvent : IEventPriority
    {
        void OnUIClick(InputEventMouseButton eventData, EventContext context);
    }

}
