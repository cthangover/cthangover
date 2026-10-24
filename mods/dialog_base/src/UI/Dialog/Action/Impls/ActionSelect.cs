using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Utils;
using Cthangover.Mods.Dialog.Action;
using Godot;
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
    /// Presents player choices by spawning answer variants in the DialogBox.
    /// WaitType is WaitSelect so the dialog pauses until the player picks an
    /// option (which triggers DialogBox.SelectVariant → Runtime.TryGoTo).
    /// Each variant's text is processed through variable substitution before
    /// display. Also sets an accompanying text message to contextualize the
    /// choice (e.g. "What will you do?").
    /// </summary>
    public class ActionSelect : ActionCommand
    {
        /// <summary>The list of selectable choice variants. Each carries display text and a GoTo target.</summary>
        public List<SelectVariant> Variants { get; set; }
        /// <summary>Context text shown alongside the choices (e.g. "What will you do?"). Supports variable substitution.</summary>
        public string Text { get; set; }
        /// <summary>Left avatar sprite ID shown during the choice. Null hides the slot.</summary>
        public string FirstAvatar { get; set; }
        /// <summary>Right avatar sprite ID shown during the choice. Null hides the slot.</summary>
        public string SecondAvatar { get; set; }

        /// <summary>Dialog pauses until the player selects a variant — then the runtime jumps to the chosen GoTo target.</summary>
        public override WaitType WaitType { get; set; } = WaitType.WaitSelect;

        /// <summary>Substitutes variables in the variants and context text, then shows the answer box; the runtime stays paused until a variant is picked.</summary>
        /// <summary>
        /// Offers the options whose conditions hold now. When none does, the choice is
        /// skipped and the dialog goes on with the next line, so a choice of only gated
        /// options never leaves the player stuck.
        /// </summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var dialogBox = runtime.DialogBox;
            if (dialogBox == null)
                return;

            var shown = Variants.Where(v => v.IsShown()).ToList();
            WaitType = shown.Count == 0 ? WaitType.NoWait : WaitType.WaitSelect;
            if (shown.Count == 0)
            {
                GameLogger.Log("DIALOG", "select: no option is shown (all conditions false), going on", LogLevel.Debug);
                return;
            }

            foreach (var variant in shown)
                variant.Text = runtime.ProcessText(variant.Text);

            dialogBox.SetVariants(shown);
            dialogBox.SetText(runtime.ProcessText(Text));
        }
    }
}
