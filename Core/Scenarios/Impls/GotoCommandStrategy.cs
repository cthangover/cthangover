using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>goto</c> DSL command. Jumps execution to a labeled anchor
    /// point (<c>:labelname</c>) in the scenario script. Supports two forms:
    /// <c>goto target_name</c> via <c>target=</c> named parameter, or
    /// <c>goto -&gt; target_name</c> via the arrow target syntax.
    /// </summary>
    public class GotoCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "goto";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (!string.IsNullOrEmpty(arrowTarget))
                dlg.GoTo(arrowTarget);
            else if (named.TryGetValue("target", out var target))
                dlg.GoTo(target);
        }
    }
}
