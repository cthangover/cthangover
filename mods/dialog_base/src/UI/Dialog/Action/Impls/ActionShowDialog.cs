
namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Makes the dialog box visible. Typically paired with ActionHideDialog
    /// for cutscenes that temporarily hide the dialog UI while animations play.
    /// </summary>
    public class ActionShowDialog : ActionCommand
    {
        /// <summary>Dialog visibility change is instant — the queue continues without pause.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Makes the dialog box chrome visible again after <see cref="ActionHideDialog"/>.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            dialogBox.Visible = true;
        }
    }
}
