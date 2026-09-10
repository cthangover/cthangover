using System.Collections.Generic;

namespace Cthangover.Core.Scenes.Parsing
{
    /// <summary>
    /// Registry of <see cref="IOperatorParser"/> instances, keyed by
    /// <see cref="ConditionParser.TokenType"/>. Looked up by
    /// <see cref="ExpressionParser"/> during Pratt precedence-climbing to
    /// resolve infix and prefix operators at runtime rather than through
    /// hardcoded switch statements.
    ///
    /// Mods register custom operators via <see cref="Register"/> during
    /// <see cref="Cthangover.Core.Mods.IModInitializer.OnModResourcesReady"/>.
    /// Registrations are global — any scenario in any mod can use the new
    /// operator once it is registered.
    /// </summary>
    public static class OperatorRegistry
    {
        private static readonly Dictionary<ConditionParser.TokenType, IOperatorParser> _parsers = new();

        /// <summary>
        /// Registers an operator parser. If an operator with the same
        /// <see cref="IOperatorParser.Token"/> is already registered, the
        /// new one is ignored.
        /// </summary>
        public static void Register(IOperatorParser parser)
        {
            if (!_parsers.ContainsKey(parser.Token))
                _parsers[parser.Token] = parser;
        }

        /// <summary>Looks up an operator parser by token type, or null.</summary>
        public static IOperatorParser Get(ConditionParser.TokenType token)
        {
            _parsers.TryGetValue(token, out var parser);
            return parser;
        }
    }
}
