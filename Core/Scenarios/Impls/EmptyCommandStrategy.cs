using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>empty</c> DSL command. Clears all visible text and avatar
    /// elements from the dialog screen without advancing the dialog. Useful for
    /// creating dramatic pauses or scene transitions between text segments.
    /// </summary>
    public class EmptyCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "empty";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.Empty();
        }
    }
}
