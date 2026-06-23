using System.Collections.Generic;
using System.Globalization;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class AnimationCommandStrategy : IScenarioCommandStrategy
    {
        public string Command => "animation";

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (named.TryGetValue("sprites", out var spritesStr))
            {
                var sprites = new List<string>(spritesStr.Split(','));
                var speed = named.TryGetValue("speed", out var sp) ? ParseFloat(sp) : 1f;
                var loop = !named.TryGetValue("loop", out var lp) || lp == "true";
                dlg.Animation(sprites, speed, 1f, loop);
            }
            else if (positional.Count > 0)
            {
                var sprite = positional[0];
                var start = named.TryGetValue("start", out var st) ? ParseInt(st) : 0;
                var count = named.TryGetValue("count", out var cn) ? ParseInt(cn) : 1;
                var speed = named.TryGetValue("speed", out var sp) ? ParseFloat(sp) : 1f;
                var loop = !named.TryGetValue("loop", out var lp) || lp == "true";
                dlg.Animation(sprite, start, count, speed, 1f, loop);
            }
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
