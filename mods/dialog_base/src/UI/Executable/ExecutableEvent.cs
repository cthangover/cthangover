using Cthangover.Core.Scenes;
using Cthangover.Mods.Dialog;
using Cthangover.Core.UI.Event;
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
    /// Base for auto-triggering dialog events. A standalone event (not inside an
    /// IExecutableEventChain) only re-fires when the condition state changes
    /// (bumped <see cref="ConditionStateMonitor.Version"/>), after its own running
    /// dialog ends, or on the first run — with a 0.5s debounce. Checks that the
    /// DialogBox is free (IsRunning == false) before building and executing a
    /// dialog queue. IsOneRun prevents re-triggering after first execution;
    /// CanRepeat allows the event to fire again on subsequent scenes/visits.
    /// CheckConditions is virtual — ScenarioEvent overrides it to evaluate script
    /// conditions.
    /// </summary>
    public partial class ExecutableEvent : Node, IOnUpdateEvent, IExecutableEvent
    {
        protected DialogBox dialogBox;
        private SceneEventController eventController;
        private float timestamp;

        // Version at which the event last ran (MinValue forces the first run).
        private long _lastRunVersion = long.MinValue;
        // "Waiting for own dialog to end" flag, set after starting a run.
        private bool _awaitingOwnEnd;

        /// <summary>
        /// <c>true</c> until <see cref="RunDialog"/> is called for the first time.
        /// Used by chains to determine whether a one-shot event has already fired.
        /// </summary>
		public bool IsFirstRun { get; private set; } = true;

        /// <summary>
        /// If <c>true</c>, the event fires exactly once and never again, even across
        /// scene revisits. After <see cref="RunDialog"/> completes, <see cref="IsFirstRun"/>
        /// becomes <c>false</c>.
        /// </summary>
		[Export] public bool IsOneRun { get; set; } = false;

        /// <summary>
        /// If <c>true</c>, the event can re-trigger on subsequent scene visits.
        /// When <c>false</c>, the event only fires on the first visit to the scene.
        /// </summary>
		[Export] public bool CanRepeat { get; set; } = true;

        /// <summary>
        /// Virtual guard evaluated before triggering. Defaults to <c>true</c>.
        /// Override to implement conditional logic (e.g. <see cref="ScenarioEvent"/>
        /// checks script conditions via <see cref="ScenarioCondition.Evaluate"/>).
        /// </summary>
		public virtual bool CheckConditions => true;

        /// <summary>
        /// Unique identifier for this event instance. Defaults to the full type name.
        /// Override to provide a stable, save-friendly ID.
        /// </summary>
		public virtual string ID => GetType().FullName;

        /// <summary>
        /// Priority within the event chain — lower values execute first. Default
        /// is <c>1</c>, placing this after high-priority handlers.
        /// </summary>
		public int Priority => 1;

		/// <summary>Finds the shared <see cref="DialogBox"/> and registers with the event controller for update polling unless a parent chain already drives this event.</summary>
		public override void _Ready()
		{
			if (dialogBox == null)
			{
				var root = GetTree()?.Root;
				if (root != null)
				{
					dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");
				}
			}

			var isInChain = GetParent() is IExecutableEventChain;
			if (!isInChain)
			{
				eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
				eventController?.AddUpdateEventListener(this);
			}

			GameLogger.Log("EVENT", $"{GetType().Name}._Ready: dialogBox={(dialogBox != null ? "found" : "null")}, chain={isInChain}, registered={!isInChain}", LogLevel.Debug);
		}

		/// <summary>Detaches from the event controller's update polling when the event leaves the tree.</summary>
		public override void _ExitTree()
		{
			if (eventController != null)
				eventController.RemoveUpdateEventListener(this);
		}

		/// <summary>
		/// Debounced poll (twice per second) that re-checks the repeat guards and
		/// the <c>ConditionStateMonitor</c> version, then triggers the dialog when
		/// the scene state changed since the last run.
		/// </summary>
		public void OnUpdate()
		{
			if (!IsFirstRun && !CanRepeat)
				return;

			if (dialogBox == null)
				return;

			if (_awaitingOwnEnd)
			{
				if (dialogBox.IsRunning)
					return;
				_awaitingOwnEnd = false;
				_lastRunVersion = ConditionStateMonitor.Version;
			}

			if (ConditionStateMonitor.Version == _lastRunVersion)
				return;

			var time = Godot.Time.GetTicksUsec() / 1_000_000.0;
			if (time - timestamp < 0.5f)
				return;
			timestamp = (float)time;

			if (dialogBox.IsRunning)
				return;

			if (RunDialog())
				_awaitingOwnEnd = true;
		}

        /// <summary>
        /// Builds a <see cref="DialogQueue"/> via <see cref="CreateDialog"/> and queues
        /// it on the <see cref="DialogBox"/>, passing <c>this</c> as the locker to
        /// block other dialog attempts. Returns <c>false</c> if the one-shot already
        /// ran, the dialog box is unavailable, or another locker is active.
        /// </summary>
		public bool RunDialog()
		{
			if (!IsFirstRun && IsOneRun)
				return false;

			if (dialogBox == null)
				return false;

			if (dialogBox.Locker != null)
				return false;

			GameLogger.Log("EVENT", $"{GetType().Name}.RunDialog: starting dialog...");

			var dlg = new DialogQueue();
			CreateDialog(dlg);

			dialogBox.SetDialogQueueAndRun(dlg, null, 0, this);
			IsFirstRun = false;
			return true;
		}

		protected virtual void CreateDialog(DialogQueue dlg)
		{
		}
	}

}
