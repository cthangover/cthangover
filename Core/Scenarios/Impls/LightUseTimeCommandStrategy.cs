using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>light_use_time</c> DSL command. Toggles whether the
    /// scene lighting should be driven by the simulated time-of-day system.
    /// Passes a boolean flag to <see cref="DialogQueue.LightUseTime"/>.
    /// </summary>
    public class LightUseTimeCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "light_use_time";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var val = positional.Count > 0 ? positional[0] : "true";
            dlg.LightUseTime(val == "true");
        }
    }
}
