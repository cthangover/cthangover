using Godot;

namespace Cthangover.Core.UI.Event
{

    public interface IOnDragEvent : IEventPriority
    {
        void OnDrag(InputEventMouseMotion eventData, EventContext context);
    }

}
