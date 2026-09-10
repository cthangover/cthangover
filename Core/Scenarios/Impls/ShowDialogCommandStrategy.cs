using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>show_dialog</c> DSL command. Makes the dialog UI panel
    /// (text box, avatars, and UI chrome) visible on screen. The inverse of
    /// <see cref="HideDialogCommandStrategy"/>.
    /// </summary>
    public class ShowDialogCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "show_dialog";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.ShowDialog();
        }
    }
}
