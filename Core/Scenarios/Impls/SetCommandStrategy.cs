using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class SetCommandStrategy : IScenarioCommandStrategy
    {
        public string Command => "set";

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var name = positional.Count > 0 ? positional[0] : "";
            var value = positional.Count > 1 ? positional[1] : "";
            dlg.Set(name, value);
        }
    }
}
