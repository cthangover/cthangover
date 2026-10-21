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
    /// Handles the <c>end</c> DSL command. Marks the end of the dialog sequence,
    /// terminating execution of the current scenario. Typically used at the final
    /// line of a scenario script to signal the dialog engine to close or transition.
    /// </summary>
    public class EndCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "end";
        /// <inheritdoc/>
        public int MaxPositional => 0;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.End();
        }
    }
}
