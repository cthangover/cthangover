using Cthangover.Mods.Dialog;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Executable
{
    /// <summary>
    /// Abstract chain base: manages a list of ExecutableEvents and exposes
    /// Play()/Stop()/IsActive for external control. GetNext() is abstract so
    /// subclasses define their own selection strategy (sequential, conditional,
    /// random). Play() calls RunDialog() on the next event, which internally
    /// calls SetDialogQueueAndRun with `this` as the locker — the chain is
    /// blocked until the dialog finishes.
    /// </summary>
    public abstract partial class ExecutableEventChainBase : Godot.Node, IExecutableEventChain
    {

        protected bool isActive = true;
        protected System.Collections.Generic.List<ExecutableEvent> chain;
        
        protected DialogBox dialogBox;

        /// <summary>
        /// Controls whether the chain is actively polling. Setting to <c>false</c>
        /// pauses event dispatch; setting to <c>true</c> resumes.
        /// </summary>
        public bool IsActive
        {
            get => isActive;
            set { isActive = value; }
        }

        /// <summary>
        /// Pauses the chain. No further events will be dispatched until
        /// <see cref="Play"/> is called.
        /// </summary>
        public void Stop()
        {
            IsActive = false;
        }

        /// <summary>
        /// Activates the chain and immediately attempts to run the next event
        /// returned by <see cref="GetNext"/>. If no event is available, the
        /// chain deactivates itself.
        /// </summary>
        public void Play()
        {
            IsActive = true;
            var next = GetNext();
            if (next == null)
            {
                IsActive = false;
                return;
            }
            next.RunDialog();
        }
        
        protected abstract ExecutableEvent GetNext();

        /// <summary>Interface implementation: casts to <see cref="ExecutableEvent"/> and registers it with the chain node.</summary>
        public virtual void AddEvent(IExecutableEvent evt)
        {
            var typed = (ExecutableEvent)evt;
            AddChild(typed);
            chain ??= new System.Collections.Generic.List<ExecutableEvent>();
            chain.Add(typed);
        }

        /// <summary>Interface implementation: frees every registered event and empties the chain list.</summary>
        public virtual void ClearEvents()
        {
            if (chain == null)
                return;
            foreach (var evt in chain)
                evt.QueueFree();
            chain.Clear();
        }
    }

}
