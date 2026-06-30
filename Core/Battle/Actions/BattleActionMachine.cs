using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.Battle.Actions
{

    /// <summary>
    /// Sequential action queue that processes one IBattleAction per frame
    /// until it signals completion (DoAction returns true), then moves to
    /// the next. Fires OnStopMachine when the queue drains naturally or
    /// is explicitly stopped — used by battle cores to detect when a turn's
    /// animation sequence is finished. Actions can be added before or during
    /// processing; the machine starts automatically once at least one action
    /// is queued and no current action is running.
    /// </summary>
	public partial class BattleActionMachine : Node
	{

		/// <summary>
		/// Pending action queue. Actions are dequeued one-by-one in _Process,
		/// starting the next as soon as the current signals completion.
		/// Can be extended while the machine is running — new actions will
		/// be processed after the current queue drains.
		/// </summary>
		public  List<IBattleAction> Actions       { get; set; } = new();
		private IBattleAction       CurrentAction { get; set; }
		private bool                _hadActions;

		/// <summary>
		/// Fired when the machine drains naturally or is explicitly stopped.
		/// Battle cores subscribe to advance the turn phase without polling.
		/// </summary>
		public event StopMachineDelegate OnStopMachine;
		
		public override void _ExitTree()
		{
			base._ExitTree();
			StopMachine();
		}

		public override void _Process(double delta)
		{
			if (Actions.Count == 0 && CurrentAction == null)
			{
				if (_hadActions)
				{
					_hadActions = false;
					StopMachine();
				}
				return;
			}
			
			var action = CurrentAction;
			if (action == null)
			{
				CurrentAction = Actions[0];
				Actions.RemoveAt(0);
				action = CurrentAction;
				_hadActions = true;
				action?.DoStart();
			}

			if (action == null)
			{
				_hadActions = false;
				StopMachine();
				return;
			}

			if (action.DoAction())
			{
				action.DoEnd();
				CurrentAction = null;
			}
		}

		/// <summary>
		/// Enqueues a single action. If the machine is idle (no current
		/// action), processing starts on the next _Process tick.
		/// </summary>
		public void AddAction(IBattleAction action)
		{
			if (action == null)
				return; 
			Actions.Add(action);
		}

		/// <summary>
		/// Batch-enqueues multiple actions. All are appended to the existing
		/// queue and processed in FIFO order on subsequent frames.
		/// </summary>
		public void AddActions(List<IBattleAction> actions)
		{
			if(actions == null || actions.Count == 0)
				return;
			Actions.AddRange(actions);
		}
		
		/// <summary>
		/// Immediately clears the queue, aborts the current action, and
		/// fires <see cref="OnStopMachine"/>. Called on _ExitTree and
		/// when the queue drains naturally.
		/// </summary>
		public void StopMachine()
		{
			CurrentAction = null;
			Actions.Clear();
			_hadActions = false;
			OnStopMachine?.Invoke();
		}

	}

}
