using Cthangover.Core.UI.Dialog;
using Godot;

namespace Cthangover.Core.UI.Executable
{

    public abstract partial class ExecutableEventChainBase : Godot.Node, IExecutableEventChain
    {

        protected bool isActive = true;
        protected System.Collections.Generic.List<ExecutableEvent> chain;
        
        protected DialogBox dialogBox;

        public bool IsActive
        {
            get => isActive;
            set { isActive = value; }
        }
        
        public void Stop()
        {
            IsActive = false;
        }

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
    }

}
