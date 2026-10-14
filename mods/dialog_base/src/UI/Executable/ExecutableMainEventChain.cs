using System.Collections.Generic;
using Cthangover.Core.Events;
using Cthangover.Mods.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Executable
{
    /// <summary>
    /// The primary event chain for a scene. Registers as IOnUpdateEvent and
    /// rescans registered ExecutableEvents only when the condition state changes
    /// (bumped <see cref="ConditionStateMonitor.Version"/>), on dialog end, or on
    /// the first scan — with a 0.5s debounce. On _Ready, auto-collects child
    /// ExecutableEvent nodes and handles deferred scene switches (pending scene
    /// name from SceneManager). Exposes LastBackgroundID so action commands can
    /// remember which background was active for scene restore on revisit.
    /// AddEvent/RemoveEvent/ClearEvents allow dynamic event management via code.
    /// Events are checked in insertion order; the first satisfying both IsOneRun
    /// and CheckConditions gets executed. A skip-guard prevents re-running the
    /// same event at the same state version, breaking self-trigger loops while
    /// preserving A→B chaining.
    /// </summary>
    public partial class ExecutableMainEventChain : Control, IExecutableEventChain, IOnUpdateEvent
	{
		private bool isActive = true;
		private List<ExecutableEvent> allEvents = new();
		private DialogBox dialogBox;
		private SceneEventController eventController;
		private float timestamp;

        // Version of the last event scan (MinValue forces the first scan).
        private long _lastScanVersion = long.MinValue;
        // Version at the moment the chain's own last-run event finished its dialog.
        private long _lastRunEndVersion = long.MinValue;
        // The last event the chain actually started.
        private ExecutableEvent _lastRunEvent;
        // "Wake up and rescan" flag, set on dialog end or new event added.
        private bool _wakeup;

        /// <summary>
        /// Stores the last background ID set by action commands (e.g. "bg_home").
        /// Used on scene revisit to restore the previously active background.
        /// </summary>
		public string LastBackgroundID { get; set; }

        /// <summary>
        /// Event priority for <see cref="SceneEventController"/> sorting. Value
        /// <c>1</c> places this chain after high-priority handlers but before
        /// general subscribers.
        /// </summary>
		public int Priority => 1;

        /// <summary>
        /// Enables or disables event polling. When <c>false</c>,
        /// <see cref="OnUpdate"/> returns immediately without dispatching events.
        /// </summary>
		public bool IsActive
		{
			get => isActive;
			set { isActive = value; }
		}

        /// <summary>
        /// Removes and frees all currently registered events from the chain via
        /// <c>QueueFree</c> and empties the internal event list.
        /// </summary>
		public void ClearEvents()
		{
			GameLogger.Log("CHAIN", $"ClearEvents: removing {allEvents.Count} event(s)");

			foreach (var evt in allEvents)
			{
				var id = evt is ScenarioEvent se ? se.Condition ?? "no_condition" : evt.GetType().Name;
				GameLogger.Log("CHAIN", $"  QueueFree: {evt.GetType().Name} [condition={id}]");
				evt.QueueFree();
			}
			allEvents.Clear();
		}

        /// <summary>
        /// Adds an <see cref="ExecutableEvent"/> to the chain and reparents it via
        /// <c>AddChild</c>. Events are dispatched in insertion order.
        /// </summary>
        /// <param name="evt">The event to register and parent to this chain.</param>
		public void AddEvent(IExecutableEvent evt)
		{
			var typed = (ExecutableEvent)evt;
			var id = typed is ScenarioEvent se ? se.Condition ?? "no_condition" : typed.GetType().Name;
			GameLogger.Log("CHAIN", $"AddEvent: {typed.GetType().Name} [condition={id}] — calling AddChild");
			AddChild(typed);
			allEvents.Add(typed);
			_wakeup = true;
			GameLogger.Log("CHAIN", $"  AddEvent: done, allEvents.Count={allEvents.Count}");
		}

        /// <summary>
        /// Removes an event from the chain and queues it for deletion. No-op if
        /// the event is not currently registered.
        /// </summary>
        /// <param name="evt">The event to unregister and free.</param>
		public void RemoveEvent(ExecutableEvent evt)
		{
			if (allEvents.Remove(evt))
			{
				evt.QueueFree();
				GameLogger.Log("CHAIN", $"removed event '{evt.GetType().Name}'");
			}
		}

		/// <summary>Finds the shared <see cref="DialogBox"/> and event controller, collects child events, publishes the shell-ready event and replays any pending scene switch.</summary>
		public override void _Ready()
		{
			GameLogger.Log("CHAIN", $"_Ready: starting, tree scene='{GetTree()?.CurrentScene?.Name}'");

			dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");

			eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
			eventController?.AddUpdateEventListener(this);
			ModEventBus.Subscribe<DialogEndedEvent>(OnDialogEnded);

			foreach (var child in GetChildren())
			{
				if (child is ExecutableEvent evt)
				{
					allEvents.Add(evt);
					GameLogger.Log("CHAIN", $"collected event '{evt.GetType().Name}'");
				}
			}

			ModEventBus.Publish(new ShellReadyEvent { Root = this });

			var sceneManager = GetNodeOrNull<SceneManager>("/root/SceneManager");
			if (sceneManager != null && !string.IsNullOrEmpty(sceneManager.PendingSceneName))
			{
				var pending = sceneManager.PendingSceneName;
				GameLogger.Log("CHAIN", $"_Ready: deferring SwitchScene('{pending}')");
				sceneManager.PendingSceneName = null;
				Callable.From(() => sceneManager.SwitchScene(pending)).CallDeferred();
			}

			GameLogger.Log("CHAIN", $"_Ready: done, allEvents={allEvents.Count}");
		}

		/// <summary>Detaches the update and dialog-end listeners from the event controller when the chain leaves the tree.</summary>
		public override void _ExitTree()
		{
			eventController?.RemoveUpdateEventListener(this);
			ModEventBus.Unsubscribe<DialogEndedEvent>(OnDialogEnded);
		}

		/// <summary>
		/// Called when any dialog queue finishes. Wakes the chain so the next event
		/// can be picked up without waiting for a state change, and — for the chain's
		/// own last-run event — records the version at its dialog end so that the
		/// state bumps it produced are considered accounted for (breaks self-loops).
		/// </summary>
		private void OnDialogEnded(DialogEndedEvent evt)
		{
			_wakeup = true;
			if (_lastRunEvent != null && evt.LockerId == _lastRunEvent.ID)
				_lastRunEndVersion = ConditionStateMonitor.Version;
		}

		/// <summary>Debounced scan that starts the next runnable event when the condition state changed since the last completed run.</summary>
		public void OnUpdate()
		{
			if (!isActive)
				return;

			var time = Godot.Time.GetTicksUsec() / 1_000_000.0;
			if (time - timestamp < 0.5f)
				return;
			timestamp = (float)time;

			if (dialogBox == null || dialogBox.IsRunning)
				return;

			var version = ConditionStateMonitor.Version;
			if (version == _lastScanVersion && !_wakeup)
				return;

			_wakeup = false;
			_lastScanVersion = version;

			var dialog = FindEvent(allEvents, _lastRunEvent, _lastRunEndVersion);
			if (dialog == null)
				return;

			if (dialog.RunDialog())
				_lastRunEvent = dialog;
		}

        /// <summary>
        /// <c>true</c> if the chain has any registered events remaining. Used to
        /// check whether a scene still has pending dialog content.
        /// </summary>
		public bool HasEvents => allEvents.Count > 0;

        /// <summary>
        /// Deactivates the chain, preventing any further event dispatch until
        /// reactivation.
        /// </summary>
		public void Stop()
		{
			isActive = false;
		}

		private ExecutableEvent FindEvent(List<ExecutableEvent> list, ExecutableEvent skipEvent, long skipEndVersion)
		{
			if (Lists.IsEmpty(list))
				return null;
			foreach (var nextEvent in list)
			{
				if (nextEvent == skipEvent && ConditionStateMonitor.Version <= skipEndVersion)
					continue;
				if (nextEvent.IsOneRun && !nextEvent.IsFirstRun)
					continue;
				if (nextEvent.CheckConditions)
					return nextEvent;
			}
			return null;
		}
	}

}
