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
        /// <summary>The dialog text to display. Supports ${var} substitution when <see cref="UseProcessText"/> is true.</summary>
        public string Text { get; set; }
        /// <summary>Left avatar sprite ID resolved through <see cref="AvatarFactory"/>. Null or empty hides the slot.</summary>
        public string FirstAvatar { get; set; }
        /// <summary>Right avatar sprite ID resolved through <see cref="AvatarFactory"/>. Null or empty hides the slot.</summary>
        public string SecondAvatar { get; set; }
        /// <summary>When true, the avatar is rendered in shader-based silhouette/hidden-color mode.</summary>
        public bool HideColor { get; set; }
        /// <summary>When true, runtime variable substitution (${var}) is applied to <see cref="Text"/> before display.</summary>
        public bool UseProcessText { get; set; }

        /// <summary>Pauses for player click/key input — the dialog waits for user acknowledgment.</summary>
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
