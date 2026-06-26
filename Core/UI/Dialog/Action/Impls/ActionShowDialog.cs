
namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Makes the dialog box visible. Typically paired with ActionHideDialog
    /// for cutscenes that temporarily hide the dialog UI while animations play.
    /// </summary>
    public class ActionShowDialog : ActionCommand
    {
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            dialogBox.Visible = true;
        }
    }
}
