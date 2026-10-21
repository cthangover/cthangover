using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Handles the <c>background_color</c> DSL command. Sets the dialog scene's
    /// background color from a named color string (e.g. <c>black</c>, <c>red</c>)
    /// or an HTML hex color (e.g. <c>#FF0000</c>). Delegates to <see cref="DialogQueue.BackgroundColor"/>.
    /// </summary>
    public class BackgroundColorCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "background_color";
        /// <inheritdoc/>
        public int MinPositional => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;

        /// <inheritdoc/>
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
