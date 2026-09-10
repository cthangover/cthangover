using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>end</c> DSL command. Marks the end of the dialog sequence,
    /// terminating execution of the current scenario. Typically used at the final
    /// line of a scenario script to signal the dialog engine to close or transition.
    /// </summary>
    public class EndCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "end";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.End();
        }
    }
}
