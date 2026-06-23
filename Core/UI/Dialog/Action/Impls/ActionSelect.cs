using System.Collections.Generic;
using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
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
