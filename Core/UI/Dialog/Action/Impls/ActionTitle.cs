
namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Sets the dialog box title text. A null title hides the title bar.
    /// NoWait — title updates are instant visual changes.
    /// </summary>
    public class ActionTitle : ActionCommand
    {
        /// <summary>Text to display in the title bar. Null hides the title bar.</summary>
        public string TitleText { get; set; }

        /// <summary>Title changes are instant visual updates — the dialog continues immediately.</summary>
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
