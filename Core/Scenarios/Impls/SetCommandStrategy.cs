using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>set</c> DSL command. Stores an arbitrary key-value pair
    /// into the dialog session's variable state via <see cref="DialogQueue.Set"/>.
    /// These variables can be read later by <c>if</c> conditions or passed to
    /// interactive actions as context.
    /// </summary>
    public class SetCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "set";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var name = positional.Count > 0 ? positional[0] : "";
            var value = positional.Count > 1 ? positional[1] : "";
            dlg.Set(name, value);
        }
    }
}
