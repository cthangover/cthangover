using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Godot;

namespace Cthangover.Core.Scenarios
{
    public class BackgroundColorCommandStrategy : IScenarioCommandStrategy
    {
        public string Command => "background_color";

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (positional.Count > 0)
                dlg.BackgroundColor(ColorFromString(positional[0]));
        }

        private static Color ColorFromString(string s)
        {
            if (s.StartsWith('#'))
                return Color.FromHtml(s.Substring(1));

            var named = s.ToLowerInvariant() switch
            {
                "black" => Colors.Black,
                "white" => Colors.White,
                "red" => Colors.Red,
                "green" => Colors.Green,
                "blue" => Colors.Blue,
                "yellow" => Colors.Yellow,
                "gray" or "grey" => Colors.Gray,
                _ => Colors.Black,
            };
            return named;
        }
    }
}
