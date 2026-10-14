using Cthangover.Core.Scenes;
using Cthangover.Mods.Dialog;
using Cthangover.Core.Utils;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Executable
{
    /// <summary>
    /// Sequential list-based chain: rescans GetNext() only when the condition
    /// state changes (bumped <see cref="ConditionStateMonitor.Version"/>), on the
    /// end of its own running dialog, or on the first scan — with a debounce.
    /// When an event runs, it's removed from the list first, so it's consumed and
    /// the chain shrinks until empty (no self-loop is possible, so no skip-guard
    /// is needed). Respects IsOneRun (skips already-run events) and
    /// CheckConditions (skips events whose conditions fail). Use for ordered
    /// event sequences where each event should play once in order.
    /// </summary>
    public partial class ExecutableEventListChain : ExecutableEventChainBase
    {

        private float checkDelay = 1f;
        private float timestamp;

        private long _lastScanVersion = long.MinValue;
        private bool _pendingDialog;
        private bool _wakeup;
        
        /// <summary>Debounced chain driver: waits out the running dialog, rescans when the condition state changed and starts the next unconsumed event.</summary>
        public override void _Process(double delta)
        {
            if (!isActive || chain.Count == 0)
            {
                IsActive = false;
                return;
            }

            if (_pendingDialog)
            {
                if (dialogBox == null || dialogBox.Locker != null)
                    return;
                _pendingDialog = false;
                _lastScanVersion = ConditionStateMonitor.Version;
                _wakeup = true;
                return;
            }

            double time = Godot.Time.GetTicksUsec() / 1_000_000.0;
            if(time - timestamp < checkDelay)
                return;
            timestamp = (float)time;

            var version = ConditionStateMonitor.Version;
            if (version == _lastScanVersion && !_wakeup)
                return;
            _wakeup = false;
            _lastScanVersion = version;

            var next = GetNext();
            if(next == null)
                return;

            chain.Remove(next);
            next.RunDialog();
            _pendingDialog = true;
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
