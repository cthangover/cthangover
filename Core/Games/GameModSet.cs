using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;

namespace Cthangover.Core.Games
{
    /// <summary>
    /// The mods one game boots with, computed from the game's current
    /// collection definition — never stored — so editing the collection
    /// changes the next launch. Members are the game's resolved graph
    /// (selected mechanics, included blocks and transitive <c>depends</c>)
    /// plus every <see cref="ModCollectionKind.Frontend"/> collection and the
    /// explicit <c>--with-mods</c> extras with their dependencies.
    /// </summary>
    public sealed class GameModSet
    {
        public ModCollection Game { get; private init; }

        /// <summary>Mod ids that stay loaded.</summary>
        public HashSet<string> ModIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Mods the game needs but which are not installed.</summary>
        public List<string> MissingMods { get; } = new();

        /// <summary>Resolver problems of the game collection (missing blocks, cycles, ...).</summary>
        public List<ModCollectionProblem> Problems { get; } = new();

        /// <summary>
        /// Pure computation over discovered mods; returns null when
        /// <paramref name="gameId"/> is not a known game collection.
        /// </summary>
        /// <summary>The front-end alone: every <see cref="ModCollectionKind.Frontend"/> collection's graph.</summary>
        public static GameModSet ComputeFrontend(
            IReadOnlyDictionary<string, ModCollection> collections,
            IReadOnlyDictionary<string, IModInfo> discovered,
            IReadOnlyCollection<string> disabled,
            IReadOnlyCollection<string> withMods = null)
        {
            var set = new GameModSet();
            var any = false;
            foreach (var frontend in (collections?.Values ?? Enumerable.Empty<ModCollection>()).Where(c => c?.Kind == ModCollectionKind.Frontend))
            {
                any = true;
                set.AddCollection(frontend, collections, discovered, disabled ?? Array.Empty<string>(), keepProblems: true);
            }

            if (!any)
                return null;

            foreach (var extra in withMods ?? Array.Empty<string>())
                AddWithDependencies(extra, set, discovered);
            return set;
        }

        public static GameModSet Compute(
            string gameId,
            IReadOnlyDictionary<string, ModCollection> collections,
            IReadOnlyDictionary<string, IModInfo> discovered,
            IReadOnlyCollection<string> disabled,
            IReadOnlyCollection<string> withMods = null)
        {
            if (string.IsNullOrEmpty(gameId) || collections == null
                || !collections.TryGetValue(gameId, out var game) || game == null
                || game.Kind != ModCollectionKind.Game)
                return null;

            var set = new GameModSet { Game = game };
            disabled ??= Array.Empty<string>();

            set.AddCollection(game, collections, discovered, disabled, keepProblems: true);
            foreach (var frontend in collections.Values.Where(c => c?.Kind == ModCollectionKind.Frontend))
                set.AddCollection(frontend, collections, discovered, disabled, keepProblems: false);

            foreach (var extra in withMods ?? Array.Empty<string>())
                AddWithDependencies(extra, set, discovered);

            return set;
        }

        private void AddCollection(
            ModCollection collection,
            IReadOnlyDictionary<string, ModCollection> collections,
            IReadOnlyDictionary<string, IModInfo> discovered,
            IReadOnlyCollection<string> disabled,
            bool keepProblems)
        {
            var graph = ModCollectionResolver.Resolve(collection, collections, discovered, disabled, new List<CatalogExportEntry>());
            foreach (var node in graph.Nodes)
            {
                if (node.State is ModCollectionNodeState.Missing or ModCollectionNodeState.InCatalog)
                {
                    if (!MissingMods.Contains(node.ModId, StringComparer.OrdinalIgnoreCase))
                        MissingMods.Add(node.ModId);
                    continue;
                }

                ModIds.Add(node.ModId);
            }

            if (keepProblems)
                Problems.AddRange(graph.Problems);
        }

        private static void AddWithDependencies(string modId, GameModSet set, IReadOnlyDictionary<string, IModInfo> discovered)
        {
            var pending = new Stack<string>();
            pending.Push(modId);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!discovered.TryGetValue(id, out var info))
                {
                    if (!set.MissingMods.Contains(id, StringComparer.OrdinalIgnoreCase))
                        set.MissingMods.Add(id);
                    continue;
                }

                if (!set.ModIds.Add(id))
                    continue;

                foreach (var dependency in (info as ModInfo)?.Manifest?.Depends ?? new List<ModDependency>())
                {
                    // An optional dependency joins only when it is installed.
                    if (dependency.IsValid && (!dependency.IsOptional || discovered.ContainsKey(dependency.ModId)))
                        pending.Push(dependency.ModId);
                }
            }
        }
    }
}
