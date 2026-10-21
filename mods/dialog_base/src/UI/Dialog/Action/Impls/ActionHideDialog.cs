
namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Hides the dialog box. Used in cutscenes where UI chrome should be
    /// temporarily hidden while animations or background transitions play.
    /// Paired with ActionShowDialog to restore visibility.
    /// </summary>
    public class ActionHideDialog : ActionCommand
    {
        /// <summary>Hides the dialog immediately — the queue continues without pause.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Hides the dialog box chrome while keeping the queue running.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            dialogBox.Visible = false;
        }
    }
}
