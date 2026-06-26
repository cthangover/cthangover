using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires on pointer click with hit-test results and press duration.
    /// Receives the full hits array so handlers can inspect what was under
    /// the cursor (useful for 3D/2D mixed scenes). Pressing time enables
    /// distinguishing taps from long-presses.
    /// </summary>
    public interface IOnPointerClickEvent : IEventPriority
    {
        void OnPointerClick(InputEventMouseButton eventData, EventContext context, Godot.Collections.Array hits, int count, float pressingTime);
    }

}
