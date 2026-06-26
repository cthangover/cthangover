
namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Sets the dialog box title text. A null title hides the title bar.
    /// NoWait — title updates are instant visual changes.
    /// </summary>
    public class ActionTitle : ActionCommand
    {
        public string TitleText { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            dialogBox.SetTitle(TitleText);
        }
    }
}
