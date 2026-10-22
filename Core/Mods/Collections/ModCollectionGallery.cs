using System;
using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Aggregated, UI-ready snapshot of one <see cref="ModCollectionKind.Game"/>
    /// collection: how many mods it needs, how many are already playable, what
    /// is missing or version-mismatched, and how big the install/enable plan
    /// would be. Built by <see cref="ModCollectionGallery"/> for the game
    /// gallery screen; every number is recomputed from the live registry,
    /// disabled store and catalog cache, so it can never go stale.
    /// <para/>
    /// <see cref="Collection"/> keeps the raw document — including its
    /// <see cref="ModCollection.Origin"/> (BuiltIn/Catalog/User) which the UI
    /// shows as the source badge, and its '@'-prefixed fields which
    /// <see cref="Name"/>/<see cref="Description"/> already carry resolved
    /// when a translator was passed.
    /// </summary>
    public class ModCollectionGameSummary
    {
        /// <summary>The resolved-against Game collection document.</summary>
        public ModCollection Collection { get; set; }

        /// <summary>Collection display name with the translator applied; raw '@key' when no translator was given.</summary>
        public string Name { get; set; }

        /// <summary>Collection display description with the translator applied; raw '@key' when no translator was given.</summary>
        public string Description { get; set; }

        /// <summary>Total mods the collection needs, transitive dependencies included.</summary>
        public int TotalMods { get; set; }

        /// <summary>Mods already installed, enabled and satisfying any version pin.</summary>
        public int InstalledCount { get; set; }

        /// <summary>Ids of the mods counted by <see cref="InstalledCount"/>.</summary>
        public List<string> ReadyMods { get; } = new();

        /// <summary>Ids that are neither installed nor available in any catalog — the plan cannot fix them.</summary>
        public List<string> MissingIds { get; } = new();

        /// <summary>Ids whose installed version (or available download) does not satisfy the collection pin.</summary>
        public List<string> MismatchIds { get; } = new();

        /// <summary>Resolution inconsistencies (missing blocks, cycles, role conflicts, unmet requirements).</summary>
        public int ProblemsCount { get; set; }

        /// <summary>Mods <see cref="ModCollectionPlan"/> would download and install.</summary>
        public int PlanInstallCount { get; set; }

        /// <summary>Installed-but-disabled mods <see cref="ModCollectionPlan"/> would re-enable.</summary>
        public int PlanEnableCount { get; set; }

        /// <summary>
        /// True when the collection can be played right now: it needs at least
        /// one mod, every needed mod is installed with a satisfying version,
        /// and the resolution reported no problems.
        /// </summary>
        public bool IsReady
            => TotalMods > 0
               && MissingIds.Count == 0
               && MismatchIds.Count == 0
               && ProblemsCount == 0;
    }

    /// <summary>
    /// Read-only aggregation layer over the merged collection store for the
    /// game gallery UI: resolves every <see cref="ModCollectionKind.Game"/>
    /// collection through <see cref="ModCollectionResolver"/> and folds the
    /// resulting graphs and plans into one <see cref="ModCollectionGameSummary"/>
    /// per game. There are no hardcoded collection ids — everything comes from
    /// the store passed by the caller (<see cref="ModCollectionStore.LoadAll"/>),
    /// so mods, catalogs and player documents all appear without code changes.
    /// <para/>
    /// Like the resolver the class is pure: every input is an argument, which
    /// keeps the summaries testable without Godot.
    /// </summary>
    public static class ModCollectionGallery
    {
        /// <summary>
        /// Builds one summary per Game collection in the store. Component and
        /// Toolset collections are building material, not playable games, and
        /// are skipped. Entries are ordered by display name, then id, so the
        /// gallery renders deterministically regardless of store order; the
        /// <paramref name="translator"/> (e.g. <see cref="CollectionText.Resolve"/>)
        /// resolves '@key' display text, and null keeps raw keys for tests;
        /// <paramref name="language"/> (the UI locale) picks the collections'
        /// own locale entries, null keeps the flat (English) text.
        /// </summary>
        public static List<ModCollectionGameSummary> Build(
            IReadOnlyDictionary<string, ModCollection> collections,
            IReadOnlyDictionary<string, IModInfo> localMods,
            IReadOnlyCollection<string> disabledIds,
            IReadOnlyList<CatalogExportEntry> catalogEntries,
            IReadOnlyCollection<string> sessionInstalled,
            Func<string, string> translator = null,
            string language = null)
        {
            var result = new List<ModCollectionGameSummary>();
            if (collections == null)
                return result;

            foreach (var pair in collections)
            {
                var collection = pair.Value;
                if (collection == null || string.IsNullOrEmpty(collection.Id))
                    continue;

                if (collection.Kind != ModCollectionKind.Game)
                    continue;

                var graph = ModCollectionResolver.Resolve(
                    collection,
                    collections,
                    localMods,
                    disabledIds,
                    catalogEntries,
                    sessionInstalled,
                    language,
                    translator);
                var plan = ModCollectionPlan.Build(graph, catalogEntries);

                var summary = new ModCollectionGameSummary
                {
                    Collection = collection,
                    Name = Translate(collection.GetLocalizedName(language), translator),
                    Description = Translate(collection.GetLocalizedDescription(language), translator),
                    TotalMods = graph.TotalCount,
                    InstalledCount = graph.InstalledCount,
                    ProblemsCount = graph.Problems.Count,
                    PlanInstallCount = plan.Install.Count,
                    PlanEnableCount = plan.Enable.Count,
                };

                foreach (var node in graph.Nodes)
                {
                    switch (node.State)
                    {
                        case ModCollectionNodeState.Installed:
                            summary.ReadyMods.Add(node.ModId);
                            break;

                        case ModCollectionNodeState.Missing:
                            summary.MissingIds.Add(node.ModId);
                            break;

                        case ModCollectionNodeState.VersionMismatch:
                            summary.MismatchIds.Add(node.ModId);
                            break;
                    }
                }

                result.Add(summary);
            }

            result.Sort((left, right) =>
            {
                var byName = string.Compare(
                    left.Name ?? left.Collection?.Id ?? string.Empty,
                    right.Name ?? right.Collection?.Id ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
                if (byName != 0)
                    return byName;

                var leftId = left.Collection?.Id ?? string.Empty;
                var rightId = right.Collection?.Id ?? string.Empty;
                return string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        /// <summary>
        /// Answers "which games use this mod?" for the mod-side cross links:
        /// returns a map with a single entry keyed by the normalized mod id
        /// (version constraints are stripped, comparison is case-insensitive)
        /// whose value lists the ids of <see cref="ModCollectionKind.Game"/>
        /// collections that reference the mod — directly through a mechanic's
        /// mod list or transitively through their includes. Entries are sorted
        /// by collection id; a game-less mod yields an empty list, and an empty
        /// <paramref name="modId"/> yields an empty map.
        /// </summary>
        public static Dictionary<string, List<string>> FindGamesUsingMod(
            IReadOnlyDictionary<string, ModCollection> collections,
            string modId)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var target = NormalizeId(modId);
            if (collections == null || string.IsNullOrEmpty(target))
                return result;

            var games = new List<string>();
            foreach (var pair in collections)
            {
                var collection = pair.Value;
                if (collection == null || string.IsNullOrEmpty(collection.Id))
                    continue;

                if (collection.Kind != ModCollectionKind.Game)
                    continue;

                if (ReferencesMod(collections, collection, target, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                    games.Add(collection.Id);
            }

            games.Sort(StringComparer.OrdinalIgnoreCase);
            result[target] = games;
            return result;
        }

        /// <summary>
        /// Walks a collection's own mechanics and, through its includes, the
        /// mechanics of every reachable block, looking for the mod id. The
        /// visited set turns include cycles into a plain "already seen" stop.
        /// </summary>
        private static bool ReferencesMod(
            IReadOnlyDictionary<string, ModCollection> collections,
            ModCollection collection,
            string modId,
            HashSet<string> visited)
        {
            if (!visited.Add(collection.Id))
                return false;

            foreach (var mechanic in collection.Mechanics ?? new List<ModCollectionMechanic>())
            {
                if (mechanic == null)
                    continue;

                foreach (var raw in mechanic.Mods ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    if (string.Equals(NormalizeId(raw), modId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            foreach (var include in collection.Includes ?? new List<ModCollectionInclude>())
            {
                if (include == null || string.IsNullOrWhiteSpace(include.Ref))
                    continue;

                var childId = NormalizeId(include.Ref);
                if (TryGetCollection(collections, childId, out var child)
                    && ReferencesMod(collections, child, modId, visited))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Strips a version constraint ("<c>app&gt;=1</c>" -> <c>app</c>), keeping comparison version-free.</summary>
        private static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var dependency = ModDependency.Parse(raw);
            return dependency.IsValid ? dependency.ModId : raw.Trim();
        }

        /// <summary>Case-insensitive lookup mirroring the resolver's tolerance for loosely-keyed stores.</summary>
        private static bool TryGetCollection(
            IReadOnlyDictionary<string, ModCollection> collections,
            string id,
            out ModCollection collection)
        {
            collection = null;
            if (collections == null || string.IsNullOrEmpty(id))
                return false;

            if (collections.TryGetValue(id, out collection) && collection != null)
                return true;

            foreach (var pair in collections)
            {
                if (pair.Value != null && string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase))
                {
                    collection = pair.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Applies the optional translator; null output keeps the raw value.</summary>
        private static string Translate(string raw, Func<string, string> translator)
        {
            if (raw == null || translator == null)
                return raw;

            return translator(raw) ?? raw;
        }
    }
}
