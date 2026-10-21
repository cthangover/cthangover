using System.Collections.Generic;
using Cthangover.Core.Localization;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Handles the <c>flag name value</c> DSL command: sets a story flag of the
    /// game through <see cref="DialogQueue.Flag"/>. Flags outlive the dialog
    /// (saved, reset by a new game) and are what <c>condition:</c> headers read;
    /// <c>set</c> is the dialog-local counterpart for <c>${var}</c> substitution.
    /// </summary>
    public class FlagCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "flag";
        /// <inheritdoc/>
        public int MinPositional => 2;
        /// <inheritdoc/>
        public int MaxPositional => 2;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
            => dlg.Flag(positional[0], positional[1]);
    }
}
