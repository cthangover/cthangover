using Cthangover.Core.Scenes;
using Cthangover.Mods.Dialog;
using Cthangover.Core.Events;
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
    /// Base for auto-triggering dialog events. Inside an IExecutableEventChain the
    /// chain decides when the event runs. A standalone event (not inside a chain)
    /// drives itself, event-driven: it asks for a deferred check when it becomes
    /// ready, when the condition state changes (<see cref="ConditionStateChangedEvent"/>)
    /// and when a dialog ends (<see cref="DialogEndedEvent"/>), and runs when the
    /// state changed since its own last run (or on the first run) and the
    /// DialogBox is free. Checks that the DialogBox is free (IsRunning == false)
    /// before building and executing a dialog queue. IsOneRun prevents
    /// re-triggering after first execution; CanRepeat allows the event to fire
    /// again on subsequent scenes/visits. CheckConditions is virtual —
    /// ScenarioEvent overrides it to evaluate script conditions.
    /// </summary>
    public partial class ExecutableEvent : Node, IExecutableEvent
    {
        protected DialogBox dialogBox;
        private bool isStandalone;
        private bool checkQueued;

        // Version at which the event last ran (MinValue forces the first run).
        private long _lastRunVersion = long.MinValue;
        // "Waiting for own dialog to end" flag, set after starting a run.
        private bool _awaitingOwnEnd;

        /// <summary>
        /// <c>true</c> until <see cref="RunDialog"/> starts the event's dialog for the first time.
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

		/// <summary>Finds the shared <see cref="DialogBox"/>; a standalone event (no parent chain) subscribes to its triggers and asks for the first check.</summary>
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

			isStandalone = GetParent() is not IExecutableEventChain;
			if (isStandalone)
			{
				ModEventBus.Subscribe<ConditionStateChangedEvent>(OnConditionStateChanged);
				ModEventBus.Subscribe<DialogEndedEvent>(OnDialogEnded);
				RequestCheck();
			}

			GameLogger.Log("EVENT", $"{GetType().Name}._Ready: dialogBox={(dialogBox != null ? "found" : "null")}, chain={!isStandalone}, standalone={isStandalone}", LogLevel.Debug);
		}

		/// <summary>Detaches a standalone event from its triggers when it leaves the tree.</summary>
		public override void _ExitTree()
		{
			if (!isStandalone)
				return;
			ModEventBus.Unsubscribe<ConditionStateChangedEvent>(OnConditionStateChanged);
			ModEventBus.Unsubscribe<DialogEndedEvent>(OnDialogEnded);
		}

		private void OnConditionStateChanged(ConditionStateChangedEvent evt)
		{
			RequestCheck();
		}

		private void OnDialogEnded(DialogEndedEvent evt)
		{
			RequestCheck();
		}

		// Coalesces triggers of one frame into one deferred check.
		private void RequestCheck()
		{
			if (checkQueued)
				return;
			checkQueued = true;
			Callable.From(Check).CallDeferred();
		}

		/// <summary>
		/// Deferred check of a standalone event: re-checks the repeat guards and the
		/// <c>ConditionStateMonitor</c> version, then triggers the dialog when the
		/// scene state changed since the last run.
		/// </summary>
		private void Check()
		{
			checkQueued = false;
			if (!IsInstanceValid(this) || !IsInsideTree())
				return;

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

			if (dialogBox.IsRunning)
				return;

			if (RunDialog())
				_awaitingOwnEnd = true;
		}

        /// <summary>
        /// Builds a <see cref="DialogQueue"/> via <see cref="CreateDialog"/> and queues
        /// it on the <see cref="DialogBox"/>, passing <c>this</c> as the locker to
        /// block other dialog attempts. Returns <c>false</c> — and leaves
        /// <see cref="IsFirstRun"/> untouched — if the one-shot already ran, the dialog
        /// box is unavailable or busy, or the box rejected the queue (e.g. inactive
        /// during a scene switch): a run that never happened is not counted.
        /// </summary>
		public bool RunDialog()
		{
			if (!IsFirstRun && IsOneRun)
				return false;

			if (dialogBox == null)
				return false;

			if (dialogBox.IsRunning)
				return false;

			GameLogger.Log("EVENT", $"{GetType().Name}.RunDialog: starting dialog...");

			var dlg = new DialogQueue();
			CreateDialog(dlg);

			if (!dialogBox.SetDialogQueueAndRun(dlg, null, StartIndex(dlg), this))
				return false;
			IsFirstRun = false;
			return true;
		}

		protected virtual void CreateDialog(DialogQueue dlg)
		{
		}

		/// <summary>Queue index the dialog starts at; 0 unless a subclass starts mid-script.</summary>
		protected virtual int StartIndex(DialogQueue dlg) => 0;
	}

}
