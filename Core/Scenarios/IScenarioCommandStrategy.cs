using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Strategy interface for handling a single DSL command type in scenario scripts.
    /// Each command word (e.g. <c>text</c>, <c>music</c>, <c>goto</c>) maps to one
    /// strategy implementation. The <see cref="ScenarioCommandStrategyFactory"/>
    /// auto-discovers all implementations via reflection and dispatches commands
    /// to the matching strategy during parsing.
    /// </summary>
    public interface IScenarioCommandStrategy
    {
        /// <summary>
        /// The lowercase DSL command word this strategy handles (e.g. <c>"text"</c>, <c>"music"</c>).
        /// </summary>
        string Command { get; }

        /// <summary>
        /// Executes the command by appending one or more dialog actions to <paramref name="dlg"/>.
        /// </summary>
        /// <param name="dlg">The dialog queue to push actions onto.</param>
        /// <param name="positional">Unnamed tokens following the command word in order.</param>
        /// <param name="named">Key-value pairs parsed from <c>name=value</c> tokens (keys are lowercased).</param>
        /// <param name="arrowTarget">The target label following a <c>-&gt;</c> arrow token, if present.</param>
        /// <param name="locale">Optional localization provider for resolving <c>key=</c> lookups.</param>
        void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale);
    }
}
