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

		public string LastBackgroundID { get; set; }

		public int Priority => 1;

		public bool IsActive
		{
			get => isActive;
			set { isActive = value; }
		}

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

		public void AddEvent(ExecutableEvent evt)
		{
			var id = evt is ScenarioEvent se ? se.Condition ?? "no_condition" : evt.GetType().Name;
			GameLogger.Log("CHAIN", $"AddEvent: {evt.GetType().Name} [condition={id}] — calling AddChild");
			AddChild(evt);
			allEvents.Add(evt);
			GameLogger.Log("CHAIN", $"  AddEvent: done, allEvents.Count={allEvents.Count}");
		}

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
				sceneManager.PendingSceneName = null;
				Callable.From(() => sceneManager.SwitchScene(pending)).CallDeferred();
			}
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

		public bool HasEvents => allEvents.Count > 0;

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
