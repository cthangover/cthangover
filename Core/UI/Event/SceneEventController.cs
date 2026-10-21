using System;
using System.Collections.Generic;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Central event bus for the UI subsystem. Maintains priority-sorted subscriber
    /// lists for Update and TimerTick events (dialog-end notifications are published
    /// on the ModEventBus as DialogEndedEvent). Subscribers are dispatched by
    /// Priority ascending — lower value = earlier execution; equal priorities keep
    /// their registration order (a new listener is inserted after the last one of
    /// its priority, the list is never re-sorted). Dispatch walks a snapshot of the
    /// list, so a listener may add or remove listeners from its callback: one added
    /// during a dispatch is first called on the next one, one removed is not called
    /// any more. Runs at ProcessPriority.MaxValue, i.e. after every other node's
    /// process callback in the frame. Timer ticks fire at 1-second intervals from
    /// an accumulator to avoid drift — from the first processed frames of the app,
    /// never paused during the launcher boot. Each tick subscriber is wrapped in
    /// try/catch to prevent one faulty handler from breaking all others. IsActive
    /// lets the bus be paused mid-scene without unregistering all listeners.
    /// <para/>
    /// Tick subscribers: see the boot-window contract on <see cref="IOnTimeEvent"/>
    /// before touching game state in <see cref="IOnTimeEvent.OnTimerTick"/>.
    /// </summary>
    public partial class SceneEventController : Node
    {
        
        private string guid;
        
        private List<IOnUpdateEvent>      OnUpdateEventList      = new();
        private List<IOnTimeEvent>        OnTimerTickEventList   = new();
        private IOnUpdateEvent[]          updateSnapshot;
        private IOnTimeEvent[]            tickSnapshot;

        /// <summary>
        /// <c>true</c> after <see cref="_ExitTree"/> runs and <see cref="ClearData"/>
        /// has been called. External code should check this before registering new
        /// listeners on a dead controller.
        /// </summary>
        public  bool                      IsDestroyed { get; private set; }

        /// <summary>
        /// Controls whether <c>_Process</c> dispatches events. When <c>false</c>,
        /// the event pump continues running but all subscriber lists are paused.
        /// Distinct from <see cref="IsActive"/> — use this for flow control.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Master enable/disable for event dispatch. When <c>false</c>,
        /// <c>_Process</c> skips all subscriber lists entirely. Use during scene
        /// transitions or loading screens to suppress UI events without
        /// unregistering listeners.
        /// </summary>
        public bool IsActive { get; set; } = true;

        private double timerAccum;
        private const double TickInterval = 1.0;
        
        /// <summary>Runs last among process callbacks (max priority) so listeners observe fully updated state, and keeps ticking while the game is paused.</summary>
        public override void _Ready()
        {
            guid = Guid.NewGuid().ToString();
            ProcessPriority = int.MaxValue;
            ProcessMode = ProcessModeEnum.Always;
        }

        /// <summary>Drops all listener registrations and marks the controller destroyed when it leaves the tree.</summary>
        public override void _ExitTree()
        {
            ClearData();
            IsDestroyed = true;
        }

        /// <summary>Accumulates time into one-second timer ticks and, when active, dispatches every registered per-frame update listener.</summary>
        public override void _Process(double delta)
        {
            timerAccum += delta;
            while (timerAccum >= TickInterval)
            {
                timerAccum -= TickInterval;
                TickTimer();
            }

            if (!IsActive)
                return;

            updateSnapshot ??= OnUpdateEventList.ToArray();
            foreach (var eventSubscriber in updateSnapshot)
            {
                // Removed by an earlier subscriber of this very dispatch.
                if (!OnUpdateEventList.Contains(eventSubscriber))
                    continue;
                // Same contract as TickTimer: one faulty (e.g. disposed across a
                // scene switch) handler must not break the remaining subscribers,
                // so each update is isolated.
                try
                {
                    eventSubscriber.OnUpdate();
                }
                catch (ObjectDisposedException ex)
                {
                    RemoveUpdateEventListener(eventSubscriber);
                    GameLogger.Log("EVENT", $"Update subscriber {eventSubscriber.GetType().Name} was disposed, unregistered: {ex.Message}", LogLevel.Error);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("EVENT", $"Update subscriber {eventSubscriber.GetType().Name} threw: {ex.Message}", LogLevel.Error);
                }
            }
        }

		private void TickTimer()
		{
			tickSnapshot ??= OnTimerTickEventList.ToArray();
			foreach (var eventSubscriber in tickSnapshot)
			{
				if (!OnTimerTickEventList.Contains(eventSubscriber))
					continue;
				try
				{
					eventSubscriber.OnTimerTick();
				}
				catch (Exception ex)
				{
					GameLogger.Log("EVENT", $"TickTimer subscriber {eventSubscriber.GetType().Name} threw: {ex.Message}", LogLevel.Error);
				}
			}
		}

        #region Add
        /// <summary>Registers an <see cref="IOnUpdateEvent"/> subscriber for per-frame updates.</summary>
        public void AddUpdateEventListener(IOnUpdateEvent listener) => AddListener(listener, OnUpdateEventList);
        /// <summary>
        /// Registers a timer-tick subscriber for ~1 second interval callbacks.
        /// Ticks start with the first processed frame and keep firing while the
        /// launcher boot compiles mods (this node is <c>ProcessMode.Always</c>) —
        /// a subscriber touching <c>GameData.Instance.Runtime</c>, the mod
        /// registry or factories inside <see cref="IOnTimeEvent.OnTimerTick"/>
        /// must guard with <c>Boot.ModsReady</c>; see the boot-window contract
        /// on <see cref="IOnTimeEvent"/> and the time mod's TimeTickController for the reference
        /// implementation.
        /// </summary>
        public void AddTimerTickEventListener(IOnTimeEvent listener) => AddListener(listener, OnTimerTickEventList);
        #endregion

        #region Remove
        /// <summary>Unregisters a per-frame update subscriber.</summary>
        public void RemoveUpdateEventListener(IOnUpdateEvent listener) => RemoveListener(listener, OnUpdateEventList);
        /// <summary>Unregisters a timer-tick subscriber.</summary>
        public void RemoveTimerTickEventListener(IOnTimeEvent listener) => RemoveListener(listener, OnTimerTickEventList);
        #endregion
        
        private void AddListener<T>(T listener, List<T> eventList) where T : class, IEventPriority
        {
            if (listener == null || eventList.Contains(listener))
                return;
            var at = eventList.Count;
            while (at > 0 && eventList[at - 1].Priority > listener.Priority)
                at--;
            eventList.Insert(at, listener);
            ResetSnapshots();
        }

        private void RemoveListener<T>(T listener, List<T> eventList) where T : class, IEventPriority
        {
            if (listener == null)
                return;
            if (eventList.Remove(listener))
                ResetSnapshots();
        }

        // A dispatch keeps iterating the array it took; the next one takes a fresh copy.
        private void ResetSnapshots()
        {
            updateSnapshot = null;
            tickSnapshot = null;
        }

        /// <summary>
        /// Empties all subscriber lists without calling callbacks. Called in
        /// <see cref="_ExitTree"/> to cleanly detach all listeners.
        /// </summary>
        public void ClearData()
        {
            OnUpdateEventList.Clear();
            OnTimerTickEventList.Clear();
            ResetSnapshots();
        }
    }
}
