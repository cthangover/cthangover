using System.Collections.Generic;
using System.Globalization;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class SwitchSceneCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        public string Command => "switch_scene";
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Scene;

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var scene = positional.Count > 0 ? positional[0] : "";
            var speed = named.TryGetValue("speed", out var sp) ? ParseFloat(sp) : 4f;
            var hidden = named.TryGetValue("hidden", out var h) && h == "true";
            dlg.SwitchScene(scene, speed, hidden);
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }
    }
}
