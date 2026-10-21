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
    /// Handles the <c>effect</c> DSL command. Applies a named visual effect/shader
    /// to the scene. Implements <see cref="ICommandReferenceMetadata"/> with
    /// <see cref="PositionalReferenceKind.Effect"/> for build-time dependency tracking.
    /// </summary>
    public class EffectCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "effect";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Effect;
        /// <inheritdoc/>
        public int MinPositional => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (positional.Count > 0)
                dlg.Effect(positional[0]);
        }
    }
}
