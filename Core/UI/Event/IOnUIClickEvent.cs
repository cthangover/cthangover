using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires on UI-layer clicks — separated from IOnPointerClickEvent to allow
    /// different handling for UI clicks vs. world-space (3D/2D) clicks.
    /// </summary>
    public interface IOnUIClickEvent : IEventPriority
    {
        void OnUIClick(InputEventMouseButton eventData, EventContext context);
    }

}
