using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Bridges the scene-event timer system to the in-game clock.
    /// Implements <see cref="Cthangover.Core.UI.Event.IOnTimeEvent"/>
    /// with priority 0 so that the tick is processed before most
    /// other listeners. The controller must be explicitly registered
    /// via <see cref="EnsureRegistered"/>, which finds the
    /// <see cref="Cthangover.Core.UI.Event.SceneEventController"/> in
    /// the scene tree and subscribes <see cref="OnTimerTick"/> to it.
    /// Registration is idempotent — subsequent calls are no-ops.
    /// </summary>
    public class TimeTickController : IOnTimeEvent
    {
        /// <summary>Executes before default-priority listeners so time
        /// updates are visible to all other tick handlers.</summary>
        public int Priority => 0;

        private bool _registered;

        /// <summary>
        /// Locates the <see cref="Cthangover.Core.UI.Event.SceneEventController"/>
        /// in the scene tree and registers this controller as a timer-tick
        /// listener. Safe to call multiple times — only the first call
        /// does work.
        /// </summary>
        public void EnsureRegistered(Node node)
        {
            if (_registered)
                return;
            var eventController = node.GetTree()?.Root?.FindChild("EventController", true, false) as SceneEventController;
            if (eventController == null)
                return;
            eventController.AddTimerTickEventListener(this);
            _registered = true;
        }

        /// <summary>
        /// Called by the event system on each timer pulse. Advances
        /// the in-game clock by one minute via
        /// <see cref="TimeData.AddTick"/>.
        /// </summary>
        public void OnTimerTick()
        {
            GameData.Instance.Runtime.Time.AddTick();
        }
    }

}
