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
    /// Handles the <c>set</c> DSL command. Stores an arbitrary key-value pair
    /// into the dialog session's variable state via <see cref="DialogQueue.Set"/>.
    /// Values are read back later through <c>${var}</c> substitution in
    /// <c>ptext</c> lines and via <see cref="Cthangover.Core.Actions.IActionContext.GetParam"/>
    /// in scenario actions.
    /// </summary>
    public class SetCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "set";
        /// <inheritdoc/>
        public int MinPositional => 2;
        /// <inheritdoc/>
        public int MaxPositional => 2;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var name = positional.Count > 0 ? positional[0] : "";
            var value = positional.Count > 1 ? positional[1] : "";
            dlg.Set(name, value);
        }
    }
}
