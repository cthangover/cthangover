using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Executable
{

    public partial class ExecutableEventListChain : ExecutableEventChainBase
    {

        private float checkDelay = 1f;
        private float timestamp;
        
        public override void _Process(double delta)
        {
            if (!isActive || chain.Count == 0)
            {
                IsActive = false;
                return;
            }

            if (dialogBox == null || dialogBox.Locker != null)
                return;
            
            double time = Time.GetTicksUsec() / 1_000_000.0;
            if(time - timestamp < checkDelay)
                return;
            timestamp = (float)time;

            var next = GetNext();
            if(next == null)
                return;

            chain.Remove(next);
            next.RunDialog();
        }

        protected override ExecutableEvent GetNext()
        {
            if (Lists.IsEmpty(chain))
                return null;
            foreach (var additional in chain)
            {
                if(additional.IsOneRun && !additional.IsFirstRun)
                    continue;
                if (additional.CheckConditions)
                    return additional;
            }
            return null;
        }

    }

}
