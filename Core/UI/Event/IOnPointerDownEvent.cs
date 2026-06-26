using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires immediately when a pointer button is pressed (before release/click).
    /// Separated from IOnPointerClickEvent to allow instant-feedback handlers
    /// (e.g. visual press state) without waiting for the full click gesture.
    /// </summary>
    public interface IOnPointerDownEvent : IEventPriority
    {
        void OnPointerDown(InputEventMouseButton eventData, EventContext context);
    }

}
