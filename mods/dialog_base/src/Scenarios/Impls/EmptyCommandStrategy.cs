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
    /// Handles the <c>empty</c> DSL command. Queues a no-op placeholder action
    /// (<see cref="ActionEmpty"/>) that is skipped instantly — the DSL-level label
    /// mechanism (<c>:label</c> via <see cref="DialogQueue.Point"/>) is built on the
    /// same action type, and a bare <c>empty</c> line works as a placeholder marker.
    /// </summary>
    public class EmptyCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "empty";
        /// <inheritdoc/>
        public int MaxPositional => 0;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.Empty();
        }
    }
}
