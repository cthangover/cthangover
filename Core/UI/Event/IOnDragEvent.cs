using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires on mouse motion while a button is held. Context allows stopping
    /// propagation so drags don't leak to underlying UI.
    /// </summary>
    public interface IOnDragEvent : IEventPriority
    {
        void OnDrag(InputEventMouseMotion eventData, EventContext context);
    }

}
