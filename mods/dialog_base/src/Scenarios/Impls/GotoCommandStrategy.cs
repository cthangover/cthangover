using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
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
    /// Handles the <c>goto</c> DSL command. Jumps execution to a labeled anchor
    /// point (<c>:labelname</c>) in the scenario script. Supports two forms:
    /// <c>goto target_name</c> via <c>target=</c> named parameter, or
    /// <c>goto -&gt; target_name</c> via the arrow target syntax.
    /// </summary>
    public class GotoCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "goto";
        /// <inheritdoc/>
        public int MinArguments => 1;
        /// <inheritdoc/>
        public int MaxPositional => 0;
        /// <inheritdoc/>
        public IReadOnlyList<string> AllowedNamed => new[] { "target" };
        /// <inheritdoc/>
        public string JumpTargetNamedKey => "target";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (!string.IsNullOrEmpty(arrowTarget))
                dlg.GoTo(arrowTarget);
            else if (named.TryGetValue("target", out var target))
                dlg.GoTo(target);
        }
    }
}
