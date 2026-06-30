using System.Collections.Generic;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// The primary event chain for a scene. Registers as IOnUpdateEvent and
    /// polls all registered ExecutableEvents every 0.5s. On _Ready, auto-collects
    /// child ExecutableEvent nodes and handles deferred scene switches (pending
    /// scene name from SceneManager). Exposes LastBackgroundID so action commands
    /// can remember which background was active for scene restore on revisit.
    /// AddEvent/RemoveEvent/ClearEvents allow dynamic event management via code.
    /// Events are checked in insertion order; the first satisfying both IsOneRun
    /// and CheckConditions gets executed.
    /// </summary>
    public partial class ExecutableMainEventChain : Control, IExecutableEventChain, IOnUpdateEvent
	{
		private bool isActive = true;
		private List<ExecutableEvent> allEvents = new();
		private DialogBox dialogBox;
		private SceneEventController eventController;
		private float timestamp;

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
		public void AddEvent(ExecutableEvent evt)
		{
			var id = evt is ScenarioEvent se ? se.Condition ?? "no_condition" : evt.GetType().Name;
			GameLogger.Log("CHAIN", $"AddEvent: {evt.GetType().Name} [condition={id}] — calling AddChild");
			AddChild(evt);
			allEvents.Add(evt);
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

		public override void _Ready()
		{
			GameLogger.Log("CHAIN", $"_Ready: starting, tree scene='{GetTree()?.CurrentScene?.Name}'");

			dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");

			eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
			eventController?.AddUpdateEventListener(this);

			foreach (var child in GetChildren())
			{
				if (child is ExecutableEvent evt)
				{
					allEvents.Add(evt);
					GameLogger.Log("CHAIN", $"collected event '{evt.GetType().Name}'");
				}
			}

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

		public override void _ExitTree()
		{
			eventController?.RemoveUpdateEventListener(this);
		}

		public void OnUpdate()
		{
			if (!isActive)
				return;

			var time = Time.GetTicksUsec() / 1_000_000.0;
			if (time - timestamp < 0.5f)
				return;
			timestamp = (float)time;

			if (dialogBox == null || dialogBox.IsRunning)
				return;

			var dialog = FindEvent(allEvents);
			if (dialog == null)
				return;

			dialog.RunDialog();
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

		private ExecutableEvent FindEvent(List<ExecutableEvent> list)
		{
			if (Lists.IsEmpty(list))
				return null;
			foreach (var nextEvent in list)
			{
				if (nextEvent.IsOneRun && !nextEvent.IsFirstRun)
					continue;
				if (nextEvent.CheckConditions)
					return nextEvent;
			}
			return null;
		}
	}

}
