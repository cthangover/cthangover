using Cthangover.Core.Utils;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Executes an arbitrary Action delegate inside the dialog queue.
    /// Allows imperative code injection (e.g. quest state changes, UI toggles)
    /// without creating a dedicated action class. NoWait so the queue advances
    /// immediately after the delegate returns.
    /// </summary>
    public class ActionRun : ActionCommand
    {
        /// <summary>Arbitrary delegate executed during dialog sequencing. Null delegates are logged and skipped.</summary>
        public System.Action Executable { get; set; }

        /// <summary>The delegate runs and the dialog advances — no pausing.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Invokes <see cref="Executable"/>; a null delegate is logged and skipped so the queue keeps flowing.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            if (Executable == null)
            {
                GameLogger.Log("DIALOG", "ActionRun: Executable delegate is null", LogLevel.Error);
                return;
            }

            Executable();
        }
    }
}
