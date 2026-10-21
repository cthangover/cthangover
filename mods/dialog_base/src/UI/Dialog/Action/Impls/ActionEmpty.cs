using Cthangover.Mods.Dialog.Action;
using Godot;
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
    /// No-op placeholder action. Used as a labeled jump target (Point) for GoTo
    /// commands — provides an addressable marker in the dialog sequence without
    /// any visible effect. NoWait so the dialog skips through labels instantly.
    /// </summary>
    public class ActionEmpty : ActionCommand
    {
        /// <summary>Label actions skip immediately so the dialog doesn't pause on markers.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>No-op: the action exists only to carry the jump-target ID.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
        }
    }
}
