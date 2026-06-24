using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class MusicCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        public string Command => "music";
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Music;

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (positional.Count > 0)
                dlg.Music(positional[0]);
        }
    }
}
