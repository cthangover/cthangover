using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>action</c> DSL command. Triggers a named interactive action
    /// (defined in the action system) and forwards any extra named parameters as
    /// context variables via <see cref="DialogQueue.Set"/>.
    /// Implements <see cref="ICommandReferenceMetadata"/> to signal that the
    /// first positional argument is an <see cref="PositionalReferenceKind.Action"/> reference.
    /// </summary>
    public class ActionCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        /// <inheritdoc/>
        public string Command => "action";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Action;

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
