using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>effect</c> DSL command. Applies a named visual effect/shader
    /// to the scene. Implements <see cref="ICommandReferenceMetadata"/> with
    /// <see cref="PositionalReferenceKind.Effect"/> for build-time dependency tracking.
    /// </summary>
    public class EffectCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata
    {
        /// <inheritdoc/>
        public string Command => "effect";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Effect;

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            if (positional.Count > 0)
                dlg.Effect(positional[0]);
        }
    }
}
