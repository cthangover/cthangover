using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>background</c> DSL command. Sets the dialog scene background
    /// image from a resource path. Implements <see cref="ICommandReferenceMetadata"/>
    /// with <see cref="PositionalReferenceKind.Background"/> so the build pipeline
    /// can track referenced background assets.
    /// </summary>
    public class BackgroundCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        /// <inheritdoc/>
        public string Command => "background";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Background;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.Background(positional.Count > 0 ? positional[0] : "");
        }
    }
}
