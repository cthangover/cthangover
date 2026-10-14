using Cthangover.Core.Factories;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// The primary dialog text display action. Sets the text and both avatar slots
    /// (left/right) on the DialogBox. WaitType is WaitClick so the dialog pauses
    /// for player input before advancing. Avatars are resolved through
    /// IAvatarProvider by string ID; a null/empty avatar hides that slot. When
    /// UseProcessText is true, runtime variables (${var}) are substituted before display.
    /// </summary>
    public class ActionText : ActionCommand
    {
        /// <summary>The dialog text to display. Supports ${var} substitution when <see cref="UseProcessText"/> is true.</summary>
        public string Text { get; set; }
        /// <summary>Left avatar sprite ID resolved through <see cref="IAvatarProvider"/>. Null or empty hides the slot.</summary>
        public string FirstAvatar { get; set; }
        /// <summary>Right avatar sprite ID resolved through <see cref="IAvatarProvider"/>. Null or empty hides the slot.</summary>
        public string SecondAvatar { get; set; }
        /// <summary>When true, the avatar is rendered in shader-based silhouette/hidden-color mode.</summary>
        public bool HideColor { get; set; }
        /// <summary>When true, runtime variable substitution (${var}) is applied to <see cref="Text"/> before display.</summary>
        public bool UseProcessText { get; set; }

        /// <summary>Pauses for player click/key input — the dialog waits for user acknowledgment.</summary>
        public override WaitType WaitType { get; set; } = WaitType.WaitClick;

        /// <summary>Sets the dialog text (optionally variable-substituted) and both avatar slots, then waits for the player's click.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            var processedText = UseProcessText ? runtime.ProcessText(Text) : Text;
            dialogBox.SetText(processedText);
            dialogBox.SetFirstAvatar(AvatarProviderRegistry.Get(FirstAvatar), HideColor);
            dialogBox.SetSecondAvatar(AvatarProviderRegistry.Get(SecondAvatar), HideColor);
        }
    }
}
