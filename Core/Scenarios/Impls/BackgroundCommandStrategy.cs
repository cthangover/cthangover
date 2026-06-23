using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class BackgroundCommandStrategy : IScenarioCommandStrategy
    {
        public string Command => "background";

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.Background(positional.Count > 0 ? positional[0] : "");
        }
    }
}
