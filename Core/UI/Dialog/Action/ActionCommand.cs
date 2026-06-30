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
        /// <summary>Unique identifier for this action. Auto-generated GUID by default; can be overridden via <see cref="SetID"/> for GoTo targeting.</summary>
        public string ID { get; set; }
        /// <summary>Whether <see cref="Construct"/> has been called and <see cref="DoConstruct"/> has completed.</summary>
        public bool IsConstructed { get; private set; }
        /// <summary>Whether <see cref="Destruct"/> has been called and this action is finished.</summary>
        public bool IsDestructed  { get; private set; }
        /// <summary>When to call <see cref="Construct"/>: OnStartQueue (preload) or OnStartAction (lazy, just before run). Default is OnStartAction.</summary>
        public virtual ConstructType ConstructType { get; set; } = ConstructType.OnStartAction;
        /// <summary>When to call <see cref="Destruct"/>: OnEndAction (immediate), OnDelayed (manual cleanup), or OnEndQueue (when dialog finishes). Default is OnEndAction.</summary>
        public virtual DestructType DestructType { get; set; } = DestructType.OnEndAction;
        /// <summary>Controls how the runtime advances past this action. Default is WaitClick (pause for user input).</summary>
        public virtual WaitType WaitType { get; set; } = WaitType.WaitClick;
        /// <summary>Duration in seconds for WaitTime. Used only when <see cref="WaitType"/> is WaitTime.</summary>
        public virtual float WaitTime { get; set; }
        /// <summary>Timestamp when <see cref="Run"/> was called. Used by WaitTime actions to measure elapsed time.</summary>
        public float StartTime { get; private set; }
        /// <summary>Action IDs whose <see cref="Destruct"/> will be triggered when this action destructs. Used for cross-action cleanup dependencies.</summary>
        public ISet<string> DelayedDestruct { get; } = new HashSet<string>();

        public ActionCommand()
        {
            ID = Guid.NewGuid().ToString();
        }
        
        /// <summary>Idempotent construction: calls <see cref="DoConstruct"/> once and sets <see cref="IsConstructed"/> to true.</summary>
        public void Construct()
        {
            if(IsConstructed) return;
            DoConstruct();
            IsConstructed = true;
        }

        /// <summary>Idempotent desctruction: calls <see cref="DoDestruct"/> once, then sets <see cref="IsConstructed"/> false and <see cref="IsDestructed"/> true.</summary>
        public void Destruct()
        {
            if(IsDestructed) return;
            DoDestruct();
            IsConstructed = false;
            IsDestructed  = true;
        }

        /// <summary>Constructs if not already done, records start time, and delegates execution to <see cref="DoRun"/>.</summary>
        public void Run(DialogRuntime runtime)
        {
            if (!IsConstructed) DoConstruct();
            StartTime = (float)(Time.GetTicksUsec() / 1_000_000.0);
            DoRun(runtime);
        }

        /// <summary>Override in subclasses to implement action-specific behavior. Receives the runtime for text processing and dialog box access.</summary>
        public abstract void DoRun(DialogRuntime runtime);
        /// <summary>Override to preload resources or register listeners. Called by <see cref="Construct"/>.</summary>
        public virtual void DoConstruct() { }
        /// <summary>Override to release resources or disconnect listeners. Called by <see cref="Destruct"/>.</summary>
        public virtual void DoDestruct() { }

        /// <summary>Fluent setter for <see cref="ID"/>. Use to assign a named target for GoTo/If commands.</summary>
        public ActionCommand SetID(string id)
        {
            ID = id;
            return this;
        }
    }
}
