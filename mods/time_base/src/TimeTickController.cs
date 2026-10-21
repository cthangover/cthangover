using Cthangover.Core.Bootstrap;
using Cthangover.Core.Settings;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Mods.Time
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
            // The SceneEventController keeps ticking (1s interval, ProcessMode
            // Always) while the launcher boot compiles mods in the background —
            // registration in GameData._Ready succeeds because the engine puts
            // every autoload into the tree before any _Ready fires. Touching
            // Runtime here would create it before the starter inventory can be
            // seeded through loaded mod factories: the seed then runs against an
            // empty registry, logs "Initialize refused" spam and permanently
            // skips the starter items. Skip boot-window ticks instead — the same
            // readiness contract GameData.Runtime enforces.
            if (!Boot.ModsReady)
                return;

            GameData.Instance.Runtime.Time.AddTick();
            ModEventBus.Publish(new TimeTickEvent
            {
                TimeTick = GameData.Instance.Runtime.Time.Tick,
                NormalizedTime = GameData.Instance.Runtime.Time.Normalized,
            });
        }
    }

}
