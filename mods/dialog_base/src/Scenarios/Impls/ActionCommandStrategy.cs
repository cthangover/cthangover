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
    /// Handles the <c>action</c> DSL command. Triggers a named interactive action
    /// (defined in the action system) and forwards any extra named parameters as
    /// context variables via <see cref="DialogQueue.Set"/>.
    /// Implements <see cref="ICommandReferenceMetadata"/> to signal that the
    /// first positional argument is an <see cref="PositionalReferenceKind.Action"/> reference.
    /// </summary>
    public class ActionCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "action";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Action;
        /// <inheritdoc/>
        public int MinPositional => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var actionName = positional.Count > 0 ? positional[0] : "";

            foreach (var kv in named)
            {
                if (kv.Key is "name" or "key") continue;
                dlg.Set(kv.Key, kv.Value);
            }

            dlg.Action(actionName);
        }
    }
}
