using System.Collections.Generic;
using Cthangover.Core.Events;
using Cthangover.Mods.Dialog;
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
    /// The primary event chain for a scene: picks the next ExecutableEvent to run
    /// in the dialog box. Event-driven — there is no per-frame work. A scan is
    /// requested when the condition state changes (<see cref="ConditionStateChangedEvent"/>),
    /// when a dialog ends (<see cref="DialogEndedEvent"/>), when an event is added,
    /// when the chain becomes ready and when it is re-activated. Requests are
    /// coalesced into one deferred scan at the end of the frame: a burst of flag
    /// changes costs one pass, the state is read after the change completed (some
    /// publishers raise their event before storing the value), and a scan never
    /// runs inside the dialog runtime that ended a queue.
    /// <para/>
    /// On _Ready the chain collects child ExecutableEvent nodes and replays a
    /// pending scene switch (pending scene name from SceneManager). Exposes
    /// LastBackgroundID so action commands can remember which background was
    /// active for scene restore on revisit. AddEvent/RemoveEvent/ClearEvents allow
    /// dynamic event management via code. Events are checked in insertion order;
    /// the first satisfying both IsOneRun and CheckConditions gets executed.
    /// <para/>
    /// Self-loop guard: the chain remembers, per event, the condition version at
    /// the moment its dialog ended, and does not run that event again until the
    /// state changed after it. An event's own state changes (made during its
    /// dialog) therefore never re-trigger it, A→B chaining still works, and two
    /// repeatable events cannot ping-pong A→B→A while nothing changes.
    /// </summary>
    public partial class ExecutableMainEventChain : Control, IExecutableEventChain
	{
		private bool isActive = true;
		private List<ExecutableEvent> allEvents = new();
		private DialogBox dialogBox;

        // Condition version at the end of each event's last dialog run by this chain.
        private readonly Dictionary<ExecutableEvent, long> endVersions = new();
        // The event whose dialog the chain started and is waiting to end.
        private ExecutableEvent running;
        // A deferred scan is already queued for this frame.
        private bool scanQueued;

        /// <summary>
        /// Stores the last background ID set by action commands (e.g. "bg_home").
        /// Used on scene revisit to restore the previously active background.
        /// </summary>
		public string LastBackgroundID { get; set; }

        /// <summary>
        /// Enables or disables event dispatch. While <c>false</c>, scans do nothing;
        /// switching back to <c>true</c> requests a scan.
        /// </summary>
		public bool IsActive
		{
			get => isActive;
			set
			{
				isActive = value;
				if (value)
					RequestScan();
			}
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
			endVersions.Clear();
			running = null;
		}

        /// <summary>
        /// Adds an <see cref="ExecutableEvent"/> to the chain and reparents it via
        /// <c>AddChild</c>. Events are dispatched in insertion order; adding one
        /// requests a scan.
        /// </summary>
        /// <param name="evt">The event to register and parent to this chain.</param>
		public void AddEvent(IExecutableEvent evt)
		{
			var typed = (ExecutableEvent)evt;
			var id = typed is ScenarioEvent se ? se.Condition ?? "no_condition" : typed.GetType().Name;
			GameLogger.Log("CHAIN", $"AddEvent: {typed.GetType().Name} [condition={id}] — calling AddChild");
			AddChild(typed);
			allEvents.Add(typed);
			RequestScan();
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
				endVersions.Remove(evt);
				if (running == evt)
					running = null;
				evt.QueueFree();
				GameLogger.Log("CHAIN", $"removed event '{evt.GetType().Name}'");
			}
		}

		/// <summary>Finds the shared <see cref="DialogBox"/>, subscribes to the scan triggers, collects child events, publishes the shell-ready event, replays any pending scene switch and requests the first scan.</summary>
		public override void _Ready()
		{
			GameLogger.Log("CHAIN", $"_Ready: starting, tree scene='{GetTree()?.CurrentScene?.Name}'");

			dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");

			ModEventBus.Subscribe<DialogEndedEvent>(OnDialogEnded);
			ModEventBus.Subscribe<ConditionStateChangedEvent>(OnConditionStateChanged);

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

			RequestScan();
			GameLogger.Log("CHAIN", $"_Ready: done, allEvents={allEvents.Count}");
		}

		/// <summary>Detaches the scan triggers when the chain leaves the tree.</summary>
		public override void _ExitTree()
		{
			ModEventBus.Unsubscribe<DialogEndedEvent>(OnDialogEnded);
			ModEventBus.Unsubscribe<ConditionStateChangedEvent>(OnConditionStateChanged);
		}

		/// <summary>
		/// Called when any dialog queue finishes. For the chain's own running event
		/// records the version at its dialog end, so the state changes it produced
		/// are accounted for (self-loop guard); then requests a scan so the next
		/// event starts right away.
		/// </summary>
		private void OnDialogEnded(DialogEndedEvent evt)
		{
			if (running != null && evt.LockerId == running.ID)
			{
				endVersions[running] = ConditionStateMonitor.Version;
				running = null;
			}
			RequestScan();
		}

		private void OnConditionStateChanged(ConditionStateChangedEvent evt)
		{
			RequestScan();
		}

		/// <summary>
		/// Asks for a scan at the end of the frame. Any number of requests within
		/// one frame result in a single scan.
		/// </summary>
		public void RequestScan()
		{
			if (scanQueued)
				return;
			scanQueued = true;
			Callable.From(Scan).CallDeferred();
		}

		/// <summary>Starts the first runnable event, unless the chain is inactive or the dialog box is busy or frozen (a dialog end or a state change asks again).</summary>
		private void Scan()
		{
			scanQueued = false;
			if (!IsInstanceValid(this) || !IsInsideTree() || !isActive)
				return;

			// Shell rebuilds after battles may make the early _Ready lookup miss.
            // Resolve again on the deferred scan, after SceneManager clears scene caches.
            if (dialogBox == null || !IsInstanceValid(dialogBox))
                dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");
			if (dialogBox == null || !IsInstanceValid(dialogBox) || !dialogBox.IsActive || dialogBox.IsRunning)
				return;

			var dialog = FindEvent();
			if (dialog == null)
				return;

			if (dialog.RunDialog())
				running = dialog;
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

		private ExecutableEvent FindEvent()
		{
			if (Lists.IsEmpty(allEvents))
				return null;
			var version = ConditionStateMonitor.Version;
			foreach (var nextEvent in allEvents)
			{
				if (endVersions.TryGetValue(nextEvent, out var endVersion) && version <= endVersion)
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
