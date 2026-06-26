using System.Collections.Generic;
using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Presents player choices by spawning answer variants in the DialogBox.
    /// WaitType is WaitSelect so the dialog pauses until the player picks an
    /// option (which triggers DialogBox.SelectVariant → Runtime.TryGoTo).
    /// Each variant's text is processed through variable substitution before
    /// display. Also sets an accompanying text message to contextualize the
    /// choice (e.g. "What will you do?").
    /// </summary>
    public class ActionSelect : ActionCommand
    {
        public List<SelectVariant> Variants { get; set; }
        public string Text { get; set; }
        public string FirstAvatar { get; set; }
        public string SecondAvatar { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.WaitSelect;

        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            foreach (var variant in Variants)
                variant.Text = runtime.ProcessText(variant.Text);

            dialogBox.SetVariants(Variants);
            dialogBox.SetText(runtime.ProcessText(Text));
        }
    }
}
