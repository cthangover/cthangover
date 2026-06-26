using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires on 3D raycast clicks. Includes the pointer event type discriminator
    /// (Down/Up/Drag/Scroll) so a single handler can process multiple phases of
    /// ray interaction. Receives hit results for object-level targeting.
    /// </summary>
    public interface IOnRayEvent : IEventPriority
    {
        void OnRayClick(InputEventMouseButton eventData, EventContext context, PointerEventType pointerEventType, Godot.Collections.Array hits, int count);
    }

}
