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
    /// Handles the <c>background</c> DSL command. Sets the dialog scene background
    /// image from a resource path. Implements <see cref="ICommandReferenceMetadata"/>
    /// with <see cref="PositionalReferenceKind.Background"/> so the build pipeline
    /// can track referenced background assets.
    /// </summary>
    public class BackgroundCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "background";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Background;
        /// <inheritdoc/>
        public int MinPositional => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.Background(positional.Count > 0 ? positional[0] : "");
        }
    }
}
