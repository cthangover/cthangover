using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class EffectCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        public string Command => "effect";
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Effect;

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (positional.Count > 0)
                dlg.Effect(positional[0]);
        }
    }
}
