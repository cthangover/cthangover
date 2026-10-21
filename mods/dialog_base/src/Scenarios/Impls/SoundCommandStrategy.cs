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
    /// Handles the <c>sound</c> DSL command. Plays a one-shot sound effect
    /// from the given resource path. Implements <see cref="ICommandReferenceMetadata"/>
    /// with <see cref="PositionalReferenceKind.Sound"/> so the build pipeline
    /// can track referenced audio assets.
    /// </summary>
    public class SoundCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "sound";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Sound;
        /// <inheritdoc/>
        public int MinPositional => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (positional.Count > 0)
                dlg.Sound(positional[0]);
        }
    }
}
