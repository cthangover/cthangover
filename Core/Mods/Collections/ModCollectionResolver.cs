using System;
using System.Collections.Generic;
using System.Linq;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Builds a <see cref="ModCollectionGraph"/> for a collection by combining
    /// three live sources: the installed-mod registry, the disabled-mods store
    /// and the merged online catalog cache. The collection file itself only
    /// declares mechanics and mods — the dependency arrows come from the mods'
    /// own <c>depends</c>, so the graph always reflects reality even when a mod
    /// author changes dependencies without touching the collection.
    /// <para/>
    /// The resolver is pure and takes its sources as arguments, which keeps the
    /// graph testable without Godot and lets the screen pass the session-install
    /// overrides. Missing dependencies of collection mods are pulled into the
    /// graph as external nodes instead of being ignored, because they are
    /// exactly what makes a collection un-installable.
    /// </summary>
    public static class ModCollectionResolver
    {
        /// <summary>
        /// Pure resolution entry point. <paramref name="sessionInstalled"/> lets
        /// the UI treat mods installed during the current session as present even
        /// though the registry only learns about them after a restart.
        /// </summary>
        public static ModCollectionGraph Resolve(
            ModCollection collection,
            IReadOnlyDictionary<string, IModInfo> localMods,
            IReadOnlyCollection<string> disabledIds,
            IReadOnlyList<CatalogExportEntry> catalogEntries,
            IReadOnlyCollection<string> sessionInstalled = null)
        {
            var graph = new ModCollectionGraph();
            if (collection == null)
                return graph;

            var catalogById = IndexCatalog(catalogEntries);
            var nodes = new Dictionary<string, ModCollectionNode>(StringComparer.OrdinalIgnoreCase);
            var edges = new HashSet<(string From, string To)>();

            var mechanics = collection.Mechanics ?? new List<ModCollectionMechanic>();
            for (var i = 0; i < mechanics.Count; i++)
            {
                var mechanic = mechanics[i];
                if (mechanic == null)
                    continue;

                if (string.IsNullOrEmpty(mechanic.Id))
                    mechanic.Id = $"mechanic_{i}";

                if (mechanic.Mods == null)
                    continue;

                foreach (var raw in mechanic.Mods)
                {
                    var dependency = ModDependency.Parse(raw);
                    var modId = dependency.IsValid ? dependency.ModId : raw?.Trim();
                    if (string.IsNullOrEmpty(modId))
                        continue;

                    if (nodes.TryGetValue(modId, out var existing))
                    {
                        // The same mod may be declared by several mechanics; the
                        // first one that names it owns the node.
                        if (existing.IsExternal)
                        {
                            existing.MechanicId = mechanic.Id;
                            existing.IsExternal = false;
                        }

                        continue;
                    }

                    var node = CreateNode(modId, mechanic.Id, dependency, false,
                        localMods, disabledIds, sessionInstalled, catalogById);
                    nodes[modId] = node;
                    graph.Nodes.Add(node);
                }
            }

            graph.Mechanics.AddRange(mechanics.Where(m => m != null));

            AddDependencyClosure(graph, nodes, edges, localMods, disabledIds, sessionInstalled, catalogById);

            foreach (var (from, to) in edges)
                graph.Edges.Add(new ModCollectionEdge { FromModId = from, ToModId = to });

            return graph;
        }

        /// <summary>
        /// Walks the dependencies of every known node, adding edges and pulling
        /// unknown dependencies into the graph. The node list grows while it is
        /// being indexed, which naturally produces the transitive closure.
        /// </summary>
        private static void AddDependencyClosure(
            ModCollectionGraph graph,
            Dictionary<string, ModCollectionNode> nodes,
            HashSet<(string From, string To)> edges,
            IReadOnlyDictionary<string, IModInfo> localMods,
            IReadOnlyCollection<string> disabledIds,
            IReadOnlyCollection<string> sessionInstalled,
            IReadOnlyDictionary<string, CatalogExportEntry> catalogById)
        {
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var node = graph.Nodes[i];
                if (!localMods.TryGetValue(node.ModId, out var info) || info is not ModInfo modInfo)
                    continue;

                var dependencies = modInfo.Manifest?.DependencyIds;
                if (dependencies == null)
                    continue;

                foreach (var dependencyId in dependencies)
                {
                    if (string.IsNullOrEmpty(dependencyId))
                        continue;

                    edges.Add((dependencyId, node.ModId));

                    if (nodes.ContainsKey(dependencyId))
                        continue;

                    var external = CreateNode(dependencyId, null, default, true,
                        localMods, disabledIds, sessionInstalled, catalogById);
                    nodes[dependencyId] = external;
                    graph.Nodes.Add(external);
                }
            }
        }

        private static ModCollectionNode CreateNode(
            string modId,
            string mechanicId,
            ModDependency dependency,
            bool external,
            IReadOnlyDictionary<string, IModInfo> localMods,
            IReadOnlyCollection<string> disabledIds,
            IReadOnlyCollection<string> sessionInstalled,
            IReadOnlyDictionary<string, CatalogExportEntry> catalogById)
        {
            var node = new ModCollectionNode
            {
                ModId = modId,
                Title = modId,
                MechanicId = mechanicId,
                IsExternal = external,
                Dependency = dependency,
            };

            if (localMods.TryGetValue(modId, out var info))
            {
                if (!string.IsNullOrEmpty(info.Name))
                    node.Title = info.Name;

                node.InstalledVersion = info.Version;

                if (disabledIds.Contains(modId))
                {
                    node.State = ModCollectionNodeState.Disabled;
                }
                else if (dependency.IsValid
                         && !string.IsNullOrEmpty(dependency.VersionRaw)
                         && !dependency.IsSatisfiedBy(info.Version ?? "0.0.0.0"))
                {
                    node.State = ModCollectionNodeState.VersionMismatch;
                }
                else
                {
                    node.State = ModCollectionNodeState.Installed;
                }

                return node;
            }

            if (sessionInstalled != null && sessionInstalled.Contains(modId))
            {
                node.State = ModCollectionNodeState.Installed;
                return node;
            }

            if (catalogById.TryGetValue(modId, out var entry)
                && entry.Downloads != null
                && entry.Downloads.Count > 0)
            {
                node.State = ModCollectionNodeState.InCatalog;
                if (!string.IsNullOrEmpty(entry.Name))
                    node.Title = entry.Name;
                return node;
            }

            node.State = ModCollectionNodeState.Missing;
            return node;
        }

        private static Dictionary<string, CatalogExportEntry> IndexCatalog(
            IReadOnlyList<CatalogExportEntry> catalogEntries)
        {
            var index = new Dictionary<string, CatalogExportEntry>(StringComparer.OrdinalIgnoreCase);
            if (catalogEntries == null)
                return index;

            foreach (var entry in catalogEntries)
            {
                if (!string.IsNullOrEmpty(entry?.Id) && !index.ContainsKey(entry.Id))
                    index[entry.Id] = entry;
            }

            return index;
        }
    }
}
