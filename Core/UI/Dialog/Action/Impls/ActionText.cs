
using Cthangover.Core.Factories.Impls;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionText : ActionCommand
    {
        public string Text { get; set; }
        public string FirstAvatar { get; set; }
        public string SecondAvatar { get; set; }
        public bool HideColor { get; set; }
        public bool UseProcessText { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.WaitClick;

        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            var processedText = UseProcessText ? runtime.ProcessText(Text) : Text;
            dialogBox.SetText(processedText);
            dialogBox.SetFirstAvatar(AvatarFactory.Instance.Get(FirstAvatar), HideColor);
            dialogBox.SetSecondAvatar(AvatarFactory.Instance.Get(SecondAvatar), HideColor);
        }
    }
}
