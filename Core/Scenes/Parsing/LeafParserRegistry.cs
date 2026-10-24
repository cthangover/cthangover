using System.Collections.Generic;
using Cthangover.Core.Mods.Unload;
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
    ///
    /// Parsers leave with their mod: <see cref="ModUnloader"/> removes every
    /// parser whose type lives in an unloading assembly, and every parser
    /// registered with that mod's id as its owner (GDScript conditions, whose
    /// parser type is the kernel's <see cref="GDLeafParser"/>). Without that a
    /// game switch kept the previous mod's parser at the front of the list,
    /// pinning its load context and shadowing the reloaded mod's own parser.
    /// </summary>
    public static class LeafParserRegistry
    {
        private static readonly List<ILeafParser> _parsers = new();
        private static readonly Dictionary<ILeafParser, string> _owners = new();

        static LeafParserRegistry()
        {
            Register(new BoolLiteralParser());
            Register(new FlagParser());
            Register(new GroupParser());
        }

		/// <summary>Appends a parser to the end of the list; <paramref name="ownerModId"/> names the mod it leaves with.</summary>
		public static void Register(ILeafParser parser, string ownerModId = null)
		{
			_parsers.Add(parser);
			Own(parser, ownerModId);
		}

		/// <summary>Inserts a parser at the front of the list so it takes priority.</summary>
		public static void RegisterFirst(ILeafParser parser, string ownerModId = null)
		{
			_parsers.Insert(0, parser);
			Own(parser, ownerModId);
		}

        /// <summary>Removes one parser; false when it was not registered.</summary>
        public static bool Unregister(ILeafParser parser)
        {
            _owners.Remove(parser);
            return _parsers.Remove(parser);
        }

        /// <summary>
        /// Removes the parsers of unloading mods: those whose type is owned by
        /// <paramref name="ownership"/> and those registered for one of
        /// <paramref name="modIds"/>. Returns how many were removed.
        /// </summary>
        public static int RemoveOwned(AssemblyOwnership ownership, IEnumerable<string> modIds)
        {
            var ids = new HashSet<string>(modIds ?? new string[0]);
            return _parsers.RemoveAll(parser =>
            {
                var owned = (ownership != null && ownership.Owns(parser.GetType()))
                            || (_owners.TryGetValue(parser, out var owner) && ids.Contains(owner));
                if (owned)
                    _owners.Remove(parser);
                return owned;
            });
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

        private static void Own(ILeafParser parser, string ownerModId)
        {
            if (!string.IsNullOrEmpty(ownerModId))
                _owners[parser] = ownerModId;
        }
    }
}
