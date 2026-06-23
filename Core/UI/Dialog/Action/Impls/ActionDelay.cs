using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionDelay : ActionCommand
    {
        public bool HiddenMode { get; set; }
        public string ShowText { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.WaitTime;
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
