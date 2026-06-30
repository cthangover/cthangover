using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>foreground</c> DSL command. Places a foreground overlay
    /// image on top of the scene (above background and characters). The first
    /// positional argument specifies the resource path to the image asset.
    /// </summary>
    public class ForegroundCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "foreground";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.Foreground(positional.Count > 0 ? positional[0] : "");
        }
    }
}
