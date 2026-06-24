using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class ActionCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        public string Command => "action";
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Action;

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
