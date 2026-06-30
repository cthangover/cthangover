using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Event;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// Dialog execution engine: manages the action queue with goto-based branching
    /// and variable substitution. Run() iterates actions in a tight loop, breaking
    /// only on wait points (WaitClick/Select/Time/Event). Text variables use the
    /// "${name}" syntax and are resolved via GetVariable/SetVariable — values are
    /// stored in a simple Dictionary, not a cascading scope, keeping lookups O(1).
    /// GoTo searches the queue linearly by action ID and jumps the index; the
    /// source action's destruct fires on jump. The isLastGoTo flag prevents double
    /// advancement when a GoTo is the last action in its branch. End() processes
    /// an optional cleanup queue (endDialogQueue) before tearing down, and raises
    /// the dialog-end event via the SceneEventController for chaining.
    /// Infinite loop protection caps at 100k iterations per Run call.
    /// </summary>
    public class DialogRuntime
    {

        private List<IActionCommand>        dialogQueue;
        private List<IActionCommand>        endDialogQueue;
        private int                         goToActionIndex;
        private int                         index;
        private bool                        isLastGoTo;
        private IDictionary<string, string> variables = new Dictionary<string, string>();

        /// <summary>The dialog box instance this runtime drives. Set when a new queue is loaded.</summary>
        public DialogBox   DialogBox { get; private set; }
        /// <summary>The currently active dialog queue. Set by <see cref="SetDialogQueueAndRun"/>.</summary>
        public DialogQueue Dialog    { get; set; }

        /// <summary>The action at the current queue index. Automatically constructs it if <see cref="ConstructType"/> is OnStartAction and it hasn't been constructed yet.</summary>
        public IActionCommand CurrentAction => GetByIndex(index);

        /// <summary>True when the current action is waiting for a player choice (<see cref="WaitType.WaitSelect"/>).</summary>
        public bool IsWaitAnswer => IsWaitType(WaitType.WaitSelect);
        /// <summary>True when the current action is waiting for a timer (<see cref="WaitType.WaitTime"/>).</summary>
        public bool IsWaitTime   => IsWaitType(WaitType.WaitTime);
        /// <summary>True when the current action is waiting for an external event (<see cref="WaitType.WaitEvent"/>).</summary>
        public bool IsWaitEvent  => IsWaitType(WaitType.WaitEvent);

        /// <summary>True when the dialog queue is exhausted (index past end or queue is empty).</summary>
        public bool IsEnd => Lists.IsEmpty(dialogQueue) || index >= dialogQueue.Count;

        /// <summary>Retrieves a stored variable value by name. Returns null if not set.</summary>
        public string GetVariable(string name)
        {
            variables.TryGetValue(name, out var val);
            return val;
        }

        /// <summary>Stores a variable for later substitution via ${name} syntax. Overwrites existing values.</summary>
        public void SetVariable(string name, string value)
        {
            variables[name] = value;
        }

        /// <summary>Scans <paramref name="text"/> for ${key} patterns and replaces them with stored variable values. Unresolved keys become "?".</summary>
        public string ProcessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var vars = text.GetIncludesInQuotes("${", "}");
            if (Lists.IsNotEmpty(vars))
                foreach (var variable in vars)
                {
                    if (!variables.TryGetValue(variable, out var val))
                        val = "?";
                    text = text.Replace("${" + variable + "}", val);
                }

            return text;
        }

        private bool IsWaitType(WaitType type)
        {
            if (Lists.IsEmpty(dialogQueue))
                return false;
            if (index < 0 || index >= dialogQueue.Count)
                return false;
            var action = dialogQueue[index];
            return action != null && action.WaitType == type;
        }

        private IActionCommand GetByIndex(int idx)
        {
            if (Lists.IsEmpty(dialogQueue))
                return null;
            if (idx < 0 || idx >= dialogQueue.Count)
                return null;
            var action = dialogQueue[idx];
            if (action != null && action.ConstructType == ConstructType.OnStartAction && !action.IsConstructed)
                action.Construct();
            return action;
        }

        /// <summary>
        /// Loads a new dialog queue and prepares for execution. Resets variables, sets the dialog box reference,
        /// ends any previous queue (destructing its actions), copies the new queue, and constructs OnStartQueue actions.
        /// Note: does NOT call Run() — the caller must call Run() separately.
        /// </summary>
        public void SetDialogQueueAndRun(DialogBox dialogBox,
                                         IEnumerable<IActionCommand> queue,
                                         IEnumerable<IActionCommand> endQueue,
                                         int startIndex = 0)
        {
            variables = new Dictionary<string, string>();
            DialogBox = dialogBox;

            this.endDialogQueue = endQueue == null ? null : endQueue.ToList();
            if (Lists.IsNotEmpty(this.dialogQueue))
                End();

            this.dialogQueue = queue.ToList();

            index = startIndex;
            foreach (var action in this.dialogQueue)
                if (action != null && action.ConstructType == ConstructType.OnStartQueue && !action.IsConstructed)
                    action.Construct();
        }

        /// <summary>
        /// Searches the queue linearly for an action with ID == <paramref name="actionID"/> and jumps the index there.
        /// Destructs the source action before jumping. If not found, calls <see cref="End"/>.
        /// <paramref name="lastGoTo"/> prevents the runtime from auto-advancing past the target when the GoTo is the last action in its branch.
        /// </summary>
        public void TryGoTo(string actionID, bool lastGoTo = false)
        {
            goToActionIndex = index;

            if (string.IsNullOrEmpty(actionID))
                return;

            for (var i = 0; i < dialogQueue.Count; i++)
            {
                var action = dialogQueue[i];
                if (action?.ID == actionID)
                {
                    index = i;
                    var actionGoTo = GetByIndex(goToActionIndex);
                    if (actionGoTo != null && actionGoTo.IsConstructed && actionGoTo.DestructType == DestructType.OnEndAction)
                        actionGoTo.Destruct();
                    this.isLastGoTo = lastGoTo;
                    return;
                }
            }
            
            GameLogger.Log("DIALOG", "Dialog goto action not found!", LogLevel.Error);

            End();
        }

        /// <summary>
        /// Main execution loop. Iterates through actions, calling <see cref="IActionCommand.Run"/> on each and advancing
        /// automatically for <see cref="WaitType.NoWait"/> actions. Stops the loop on any wait-type action (WaitClick, WaitSelect,
        /// WaitTime, WaitEvent) so the runtime yields control until the condition is satisfied. Protected against infinite loops
        /// with a 100,000-iteration cap.
        /// </summary>
        public void Run()
        {
            var iteration = 0;
            for (;;)
            {
                var action = CurrentAction;
                if (action == null)
                {
                    GameLogger.Log("DIALOG", "Run: action is null, ending", LogLevel.Debug);

                    End();
                    return;
                }

                GameLogger.Log("DIALOG", $"Run: idx={index}/{dialogQueue.Count-1} type={action.GetType().Name} wait={action.WaitType}", LogLevel.Debug);
                
                action.Run(this);

                if (iteration++ > 100000)
                {
                    GameLogger.Log("DIALOG", "Run: infinite loop protection (100k iterations)", LogLevel.Warning);
                    break;
                }

                switch (action.WaitType)
                {
                    case WaitType.WaitClick:
                    case WaitType.WaitSelect:
                    case WaitType.WaitTime:
                    case WaitType.WaitEvent:
                        break;
                    case WaitType.NoWait:
                        Next();
                        continue;
                }

                break;
            }
        }

        /// <summary>
        /// Advances the queue index by one. Destructs the current action if its <see cref="DestructType"/> is OnEndAction.
        /// Processes any delayed-destruct targets registered by the current action. Calls <see cref="End"/> if the queue is exhausted.
        /// Respects <see cref="isLastGoTo"/> — if a GoTo was the final action, the index is not incremented.
        /// </summary>
        public void Next()
        {
            if (isLastGoTo)
            {
                GameLogger.Log("DIALOG", "Next: last goto, returning");
                isLastGoTo = false;
                return;
            }

            var action = CurrentAction;
            if (action != null && action.IsConstructed && action.DestructType == DestructType.OnEndAction)
                action.Destruct();

            if (action != null && Lists.IsNotEmpty(dialogQueue) && Lists.IsNotEmpty(action.DelayedDestruct))
                foreach (var item in dialogQueue)
                    if (action.DelayedDestruct.Contains(item.ID))
                        item.Destruct();

            if (Lists.IsEmpty(dialogQueue) || index < 0 || index >= dialogQueue.Count)
            {
                GameLogger.Log("DIALOG", "Next: queue exhausted, ending");
                End();
            }

            index++;
        }

        /// <summary>
        /// Terminates the dialog queue. Destructs all remaining constructed actions, hides the dialog box, processes the
        /// end-dialog cleanup queue (if any), and raises the dialog-end event via <see cref="SceneEventController"/>.
        /// Guards against end-queue actions starting a new dialog mid-cleanup by checking if the queue changed.
        /// </summary>
        public void End()
        {
            GameLogger.Log("DIALOG", $"End: {dialogQueue?.Count ?? 0} actions in queue, {(endDialogQueue?.Count ?? 0)} end actions");

            if (Lists.IsNotEmpty(dialogQueue))
            {
                foreach (var action in dialogQueue)
                    if (action != null && action.IsConstructed)
                        action.Destruct();
                dialogQueue.Clear();
            }

            if (DialogBox != null)
			{
				DialogBox.Locker = null;
				DialogBox.Hide();
			}

            if (Lists.IsNotEmpty(endDialogQueue))
            {
                GameLogger.Log("DIALOG", "End: processing end actions...");
                var oldQueue = dialogQueue;
                foreach (var action in endDialogQueue)
                {
                    if (action == null || action.IsDestructed)
                        continue;
                    if (!action.IsConstructed) action.Construct();
                    action.Run(this);
                    action.Destruct();
                }
                endDialogQueue.Clear();

                if (dialogQueue != oldQueue)
                {
                    GameLogger.Log("DIALOG", "End: new dialog started during end actions, skipping cleanup", LogLevel.Warning);
                    return;
                }
            }

            dialogQueue    = null;
            endDialogQueue = null;
            index          = 0;

            if (DialogBox != null)
            {
                var ec = SceneContextNode.FindNode<SceneEventController>("/root/EventController");
                if (ec == null)
                    ec = DialogBox.GetNodeOrNull<SceneEventController>("/root/EventController");
                ec?.EndDialog(Dialog, this, DialogBox.Locker);
            }
        }

    }

}
