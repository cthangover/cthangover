using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class GotoCommandStrategy : IScenarioCommandStrategy
    {
        public string Command => "goto";

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (!string.IsNullOrEmpty(arrowTarget))
                dlg.GoTo(arrowTarget);
            else if (named.TryGetValue("target", out var target))
                dlg.GoTo(target);
        }
    }
}
