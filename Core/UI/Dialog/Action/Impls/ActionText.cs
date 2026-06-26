using Cthangover.Core.Factories.Impls;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// The primary dialog text display action. Sets the text and both avatar slots
    /// (left/right) on the DialogBox. WaitType is WaitClick so the dialog pauses
    /// for player input before advancing. Avatars are resolved through AvatarFactory
    /// by string ID; a null/empty avatar hides that slot. When UseProcessText is
    /// true, runtime variables (${var}) are substituted before display.
    /// </summary>
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
