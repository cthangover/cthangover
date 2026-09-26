using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>hide_dialog</c> DSL command. Hides the dialog UI panel
    /// (text box, avatars, and UI chrome) from the screen. The inverse of
    /// <see cref="ShowDialogCommandStrategy"/>.
    /// </summary>
    public class HideDialogCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "hide_dialog";
        /// <inheritdoc/>
        public int MaxPositional => 0;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.HideDialog();
        }
    }
}
