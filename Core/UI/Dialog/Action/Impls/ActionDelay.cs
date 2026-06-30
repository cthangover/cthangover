using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
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
