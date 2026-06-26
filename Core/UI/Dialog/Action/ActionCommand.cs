using System;
using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action
{
    /// <summary>
    /// Base for all dialog actions with a lifecycle of Construct → Run → Destruct.
    /// Each action gets an auto-generated GUID for targeting by GoTo/If commands.
    /// The three timing enums control when Construct and Destruct fire relative to
    /// the action's lifecycle and queue life, enabling both immediate teardown
    /// (OnEndAction) and deferred cleanup (OnEndQueue). WaitType determines whether
    /// the runtime pauses here (WaitClick/Select/Time/Event) or advances immediately
    /// (NoWait). DelayedDestruct is a set of action IDs whose Destruct should be
    /// called after this action's own destruct — used for cross-action cleanup
    /// dependencies where one action's visual state must outlast another.
    /// </summary>
    public abstract class ActionCommand : IActionCommand
    {
        public string ID { get; set; }
        public bool IsConstructed { get; private set; }
        public bool IsDestructed  { get; private set; }
        public virtual ConstructType ConstructType { get; set; } = ConstructType.OnStartAction;
        public virtual DestructType DestructType { get; set; } = DestructType.OnEndAction;
        public virtual WaitType WaitType { get; set; } = WaitType.WaitClick;
        public virtual float WaitTime { get; set; }
        public float StartTime { get; private set; }
        public ISet<string> DelayedDestruct { get; } = new HashSet<string>();

        public ActionCommand()
        {
            ID = Guid.NewGuid().ToString();
        }
        
        public void Construct()
        {
            if(IsConstructed) return;
            DoConstruct();
            IsConstructed = true;
        }

        public void Destruct()
        {
            if(IsDestructed) return;
            DoDestruct();
            IsConstructed = false;
            IsDestructed  = true;
        }

        public void Run(DialogRuntime runtime)
        {
            if (!IsConstructed) DoConstruct();
            StartTime = (float)(Time.GetTicksUsec() / 1_000_000.0);
            DoRun(runtime);
        }

        public abstract void DoRun(DialogRuntime runtime);
        public virtual void DoConstruct() { }
        public virtual void DoDestruct() { }

        public ActionCommand SetID(string id)
        {
            ID = id;
            return this;
        }
    }
}
