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
    /// Waits for a fixed duration. When HiddenMode is true, hides the dialog
    /// body for the duration (for dramatic pauses or cutscene beats). ShowText,
    /// if set, displays a temporary message during the wait. DoDestruct is a
    /// no-op because body visibility is implicitly restored on next Show.
    /// </summary>
    public class ActionDelay : ActionCommand
    {
        /// <summary>When true, hides the dialog body for the duration of the wait (dramatic pauses, cutscene beats).</summary>
        public bool HiddenMode { get; set; }
        /// <summary>Optional temporary text displayed while waiting. Set to null to keep the current dialog text.</summary>
        public string ShowText { get; set; }

        /// <summary>Pauses the dialog until <see cref="WaitTime"/> elapses.</summary>
        public override WaitType WaitType { get; set; } = WaitType.WaitTime;
        /// <summary>Duration of the pause in seconds. Default 1.0.</summary>
        public override float WaitTime { get; set; } = 1f;

        /// <summary>Shows the optional <see cref="ShowText"/> and, in <see cref="HiddenMode"/>, hides the dialog body for the pause duration.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            if (ShowText != null)
                dialogBox.SetText(ShowText);

            if (HiddenMode)
                dialogBox.Body.SetDeferred("visible", false);
        }

        /// <summary>No-op: the dialog body hidden by <see cref="HiddenMode"/> is restored implicitly by the next show.</summary>
        public override void DoDestruct()
        {
            base.DoDestruct();
            if (HiddenMode)
            {
                // Body restored on next Show
            }
        }
    }
}
