using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Handles the <c>foreground</c> DSL command. Places a foreground overlay
    /// image on top of the scene (above background and characters). The first
    /// positional argument specifies the resource path to the image asset.
    /// </summary>
    public class ForegroundCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "foreground";
        /// <inheritdoc/>
        public int MinPositional => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.Foreground(positional.Count > 0 ? positional[0] : "");
        }
    }
}
