using System.Collections.Generic;
using Cthangover.Core.Scenes.Parsing.Impls;

namespace Cthangover.Core.Scenes.Parsing
{
    /// <summary>
    /// Ordered registry of <see cref="ILeafParser"/> instances, queried by
    /// <see cref="ExpressionParser"/> when the next token does not match any
    /// registered operator.
    ///
    /// Parsers are tried in registration order — the first whose
    /// <see cref="ILeafParser.CanParse"/> returns true wins. Mods register
    /// custom leaf parsers via <see cref="Register"/> (or
    /// <see cref="RegisterFirst"/> to take priority). Core parsers
    /// (booleans, flags, groups) are registered at static init; the quest
    /// parser registers itself from the quests engine mod at
    /// <c>IModInitializer.OnModLoaded</c> — the parser set is fully
    /// extensible and the core hardcodes no gameplay parsers.
    /// </summary>
    public static class LeafParserRegistry
    {
        private static readonly List<ILeafParser> _parsers = new();

        static LeafParserRegistry()
        {
            Register(new BoolLiteralParser());
            Register(new FlagParser());
            Register(new GroupParser());
        }

		/// <summary>Appends a parser to the end of the list.</summary>
		public static void Register(ILeafParser parser)
		{
			_parsers.Add(parser);
		}

		/// <summary>Inserts a parser at the front of the list so it takes priority.</summary>
		public static void RegisterFirst(ILeafParser parser)
		{
			_parsers.Insert(0, parser);
		}

        /// <summary>
        /// Returns the first registered parser whose <see cref="ILeafParser.CanParse"/>
        /// returns true for the current token position, or null.
        /// </summary>
        public static ILeafParser FindParser(TokenPeeker peeker)
        {
            foreach (var parser in _parsers)
            {
                if (parser.CanParse(peeker))
                    return parser;
            }
            return null;
        }
    }
}
