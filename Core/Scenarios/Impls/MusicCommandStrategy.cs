using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>music</c> DSL command. Starts playing a background music
    /// track from the given resource path. Implements <see cref="ICommandReferenceMetadata"/>
    /// with <see cref="PositionalReferenceKind.Music"/> so the build pipeline
    /// can track referenced audio assets.
    /// </summary>
    public class MusicCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        /// <inheritdoc/>
        public string Command => "music";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Music;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (positional.Count > 0)
                dlg.Music(positional[0]);
        }
    }
}
