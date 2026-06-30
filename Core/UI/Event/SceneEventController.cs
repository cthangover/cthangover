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
    /// lists for Update, TimerTick, DialogStart, and DialogEnd events. When
    /// subscribers are added or removed, the list is re-sorted by Priority
    /// (ascending — lower value = earlier execution). Runs at ProcessPriority.MaxValue
    /// to execute before other nodes. Timer ticks fire at 1-second intervals from
    /// an accumulator to avoid drift. Each tick subscriber is wrapped in try/catch
    /// to prevent one faulty handler from breaking all others. IsActive lets the
    /// bus be paused mid-scene without unregistering all listeners.
    /// </summary>
    public partial class SceneEventController : Node
    {
        
        private string guid;
        
        private List<IOnUpdateEvent>      OnUpdateEventList      = new();
        private List<IOnDialogStartEvent> OnDialogStartEventList = new();
        private List<IOnDialogEndEvent>   OnDialogEndEventList   = new();
        private List<IOnTimeEvent>        OnTimerTickEventList   = new();

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
        
        public override void _Ready()
        {
            guid = Guid.NewGuid().ToString();
            ProcessPriority = int.MaxValue;
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _ExitTree()
        {
            ClearData();
            IsDestroyed = true;
        }

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

            for (int i = OnUpdateEventList.Count - 1; i > -1; i--)
            {
                var eventSubscriber = OnUpdateEventList[i];
                if (eventSubscriber == null)
                {
                    OnUpdateEventList.RemoveAt(i);
                    continue;
                }
                eventSubscriber.OnUpdate();
            }
        }

		private void TickTimer()
		{
			for (int i = OnTimerTickEventList.Count - 1; i > -1; i--)
			{
				var eventSubscriber = OnTimerTickEventList[i];
				if (eventSubscriber == null)
				{
					OnTimerTickEventList.RemoveAt(i);
					continue;
				}
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

        /// <summary>
        /// Fires <see cref="IOnDialogStartEvent.OnDialogStart"/> on all registered
        /// subscribers. Called by <see cref="DialogBox"/> when a dialog queue begins.
        /// </summary>
        /// <param name="dialog">The dialog queue that is starting.</param>
        /// <param name="runtime">The dialog runtime state.</param>
        /// <param name="executableEvent">The event that triggered this dialog.</param>
        public void StartDialog(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent)
        {
            for (int i = OnDialogStartEventList.Count - 1; i > -1; i--)
            {
                var eventSubscriber = OnDialogStartEventList[i];
                if (eventSubscriber == null)
                {
                    OnDialogStartEventList.RemoveAt(i);
                    continue;
                }
                eventSubscriber.OnDialogStart(dialog, runtime, executableEvent);
            }
        }

        /// <summary>
        /// Fires <see cref="IOnDialogEndEvent.OnDialogEnd"/> on all registered
        /// subscribers. Called by <see cref="DialogBox"/> when a dialog queue finishes.
        /// </summary>
        /// <param name="dialog">The dialog queue that ended.</param>
        /// <param name="runtime">The dialog runtime state at end.</param>
        /// <param name="executableEvent">The event that triggered this dialog.</param>
        public void EndDialog(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent)
        {
            for (int i = OnDialogEndEventList.Count - 1; i > -1; i--)
            {
                var eventSubscriber = OnDialogEndEventList[i];
                if (eventSubscriber == null)
                {
                    OnDialogEndEventList.RemoveAt(i);
                    continue;
                }
                eventSubscriber.OnDialogEnd(dialog, runtime, executableEvent);
            }
        }

        #region Add
        /// <summary>Registers an <see cref="IOnUpdateEvent"/> subscriber for per-frame updates.</summary>
        public void AddUpdateEventListener(IOnUpdateEvent listener) => AddListener(listener, OnUpdateEventList);
        /// <summary>Registers a dialog-start subscriber notified when a dialog queue begins.</summary>
        public void AddDialogStartEventListener(IOnDialogStartEvent listener) => AddListener(listener, OnDialogStartEventList);
        /// <summary>Registers a dialog-end subscriber notified when a dialog queue finishes.</summary>
        public void AddDialogEndEventListener(IOnDialogEndEvent listener) => AddListener(listener, OnDialogEndEventList);
        /// <summary>Registers a timer-tick subscriber for ~1 second interval callbacks.</summary>
        public void AddTimerTickEventListener(IOnTimeEvent listener) => AddListener(listener, OnTimerTickEventList);
        #endregion

        #region Remove
        /// <summary>Unregisters a per-frame update subscriber.</summary>
        public void RemoveUpdateEventListener(IOnUpdateEvent listener) => RemoveListener(listener, OnUpdateEventList);
        /// <summary>Unregisters a dialog-start subscriber.</summary>
        public void RemoveDialogStartEventListener(IOnDialogStartEvent listener) => RemoveListener(listener, OnDialogStartEventList);
        /// <summary>Unregisters a dialog-end subscriber.</summary>
        public void RemoveDialogEndEventListener(IOnDialogEndEvent listener) => RemoveListener(listener, OnDialogEndEventList);
        /// <summary>Unregisters a timer-tick subscriber.</summary>
        public void RemoveTimerTickEventListener(IOnTimeEvent listener) => RemoveListener(listener, OnTimerTickEventList);
        #endregion
        
        private void AddListener<T>(T listener, List<T> eventList) where T : class, IEventPriority
        {
            if (listener == null || eventList.Contains(listener))
                return;
            eventList.Add(listener);
            eventList.Sort((o1, o2) => o1.Priority - o2.Priority);
        }

        private void RemoveListener<T>(T listener, List<T> eventList) where T : class, IEventPriority
        {
            if (listener == null)
                return;
            if (eventList.Remove(listener))
                eventList.Sort((o1, o2) => o1.Priority - o2.Priority);
        }

        /// <summary>
        /// Empties all subscriber lists without calling callbacks. Called in
        /// <see cref="_ExitTree"/> to cleanly detach all listeners.
        /// </summary>
        public void ClearData()
        {
            OnUpdateEventList.Clear();
            OnDialogStartEventList.Clear();
            OnDialogEndEventList.Clear();
            OnTimerTickEventList.Clear();
        }
    }
}
