using System.Collections.Generic;
using System.Globalization;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>animation</c> DSL command. Supports two modes:
    /// (a) multiple sprites via <c>sprites=</c> named parameter, or
    /// (b) a single sprite base with <c>start</c>, <c>count</c>, <c>speed</c>, and <c>loop</c> controls.
    /// Delegates to <see cref="DialogQueue.Animation"/>.
    /// </summary>
    public class AnimationCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "animation";

        /// <inheritdoc/>
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
