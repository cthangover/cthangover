using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Event;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog
{

    public class DialogRuntime
    {

        private List<IActionCommand>        dialogQueue;
        private List<IActionCommand>        endDialogQueue;
        private int                         goToActionIndex;
        private int                         index;
        private bool                        isLastGoTo;
        private IDictionary<string, string> variables = new Dictionary<string, string>();

        public DialogBox   DialogBox { get; private set; }
        public DialogQueue Dialog    { get; set; }

		public IActionCommand CurrentAction => GetByIndex(index);

        public bool IsWaitAnswer => IsWaitType(WaitType.WaitSelect);
        public bool IsWaitTime   => IsWaitType(WaitType.WaitTime);
        public bool IsWaitEvent  => IsWaitType(WaitType.WaitEvent);

        public bool IsEnd => Lists.IsEmpty(dialogQueue) || index >= dialogQueue.Count;

        public string GetVariable(string name)
        {
            variables.TryGetValue(name, out var val);
            return val;
        }

        public void SetVariable(string name, string value)
        {
            variables[name] = value;
        }

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
