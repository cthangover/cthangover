using System;
using System.Collections.Generic;
using System.Linq;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Builds a <see cref="ModCollectionGraph"/> for a collection by combining
    /// four live sources: the installed-mod registry, the disabled-mods store,
    /// the merged online catalog cache and — for collections composed of other
    /// collections — the merged collection registry. The collection file itself
    /// only declares includes and mechanics; the dependency arrows come from the
    /// mods' own <c>depends</c>, so the graph always reflects reality even when a
    /// mod author changes dependencies without touching the collection.
    /// <para/>
    /// The resolver is pure and takes its sources as arguments, which keeps the
    /// graph testable without Godot. Includes are expanded recursively into
    /// <see cref="ModCollectionGraph.Groups"/>: each included block becomes one
    /// contour with its mods flattened inside (nested blocks collapse into the
    /// nearest ancestor so the editor always shows a two-level pyramid), while
    /// the root's own mechanics become leaf groups. Cycles, missing blocks, role
    /// conflicts and unmet capability requirements are reported as
    /// <see cref="ModCollectionProblem"/> instead of throwing, because they are
    /// exactly what the player has to fix on the scheme.
    /// </summary>
    public static class ModCollectionResolver
    {
        /// <summary>
        /// Resolves a standalone collection without a collection registry.
        /// Include references are reported as missing blocks; this overload keeps
        /// existing callers and tests working unchanged.
        /// </summary>
        public static ModCollectionGraph Resolve(
            ModCollection collection,
            IReadOnlyDictionary<string, IModInfo> localMods,
            IReadOnlyCollection<string> disabledIds,
            IReadOnlyList<CatalogExportEntry> catalogEntries,
            IReadOnlyCollection<string> sessionInstalled = null,
            Func<string, string> translator = null)
            => Resolve(collection, null, localMods, disabledIds, catalogEntries, sessionInstalled, null, translator);

        /// <summary>
        /// Pure resolution entry point. <paramref name="collections"/> is the
        /// merged collection store keyed by id (user over built-in over catalog,
        /// as produced by <see cref="ModCollectionStore.LoadAll"/>);
        /// <paramref name="sessionInstalled"/> lets the UI treat mods installed
        /// during the current session as present even though the registry only
        /// learns about them after a restart. <paramref name="translator"/> is
        /// applied to group display names and descriptions after the usual
        /// locale/flat-field choice, letting the UI resolve
        /// <c>'@'</c>-prefixed translation keys (pass
        /// <see cref="CollectionText.Resolve"/> in Godot context); null keeps
        /// raw values, which keeps headless tests unchanged.
        /// </summary>
        public static ModCollectionGraph Resolve(
            ModCollection collection,
            IReadOnlyDictionary<string, ModCollection> collections,
            IReadOnlyDictionary<string, IModInfo> localMods,
            IReadOnlyCollection<string> disabledIds,
            IReadOnlyList<CatalogExportEntry> catalogEntries,
            IReadOnlyCollection<string> sessionInstalled = null,
            string language = null,
            Func<string, string> translator = null)
        {
            var graph = new ModCollectionGraph();
            if (collection == null)
                return graph;

            var catalogById = IndexCatalog(catalogEntries);
            var nodes = new Dictionary<string, ModCollectionNode>(StringComparer.OrdinalIgnoreCase);
            var edges = new HashSet<(string From, string To)>();
            var provides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resolved = new List<ModCollection>();

            var mechanics = collection.Mechanics ?? new List<ModCollectionMechanic>();
            for (var i = 0; i < mechanics.Count; i++)
            {
                var mechanic = mechanics[i];
                if (mechanic == null)
                    continue;

                if (string.IsNullOrEmpty(mechanic.Id))
                    mechanic.Id = $"mechanic_{i}";
            }

            graph.Mechanics.AddRange(mechanics.Where(m => m != null));

            var context = new ResolutionContext
            {
                Graph = graph,
                Nodes = nodes,
                Edges = edges,
                Provides = provides,
                Resolved = resolved,
                Collections = collections,
                LocalMods = localMods,
                DisabledIds = disabledIds,
                SessionInstalled = sessionInstalled,
                CatalogById = catalogById,
                Language = language,
                Translator = translator,
            };

            AddProvides(collection, provides);
            resolved.Add(collection);

            ResolveIncludes(collection, context, new List<string> { collection.Id });

            foreach (var mechanic in graph.Mechanics)
            {
                var selected = mechanic.IsSelected;
                var group = new ModCollectionGroup
                {
                    Key = $"m:{collection.Id}:{mechanic.Id}",
                    IsInclude = false,
                    CollectionId = collection.Id,
                    ParentCollectionId = collection.Id,
                    MechanicId = mechanic.Id,
                    Name = Translate(mechanic.GetLocalizedName(language), context.Translator),
                    Description = Translate(mechanic.GetLocalizedDescription(language), context.Translator),
                    Role = mechanic.Role,
                    Optional = mechanic.Optional,
                    Selected = selected,
                };
                graph.Groups.Add(group);

                // A turned-off contour stays on the canvas dimmed (no chips) so
                // the player can always re-enable it; its mods must not leak
                // into the node list or the install plan while it is off.
                if (!selected)
                    continue;

                foreach (var raw in mechanic.Mods ?? new List<string>())
                    AddDeclaredMod(raw, collection.Id, mechanic, group, context);
            }

            AddDependencyClosure(graph, nodes, edges, localMods, disabledIds, sessionInstalled, catalogById);

            foreach (var (from, to) in edges)
                graph.Edges.Add(new ModCollectionEdge { FromModId = from, ToModId = to });

            CheckRequirements(graph, resolved, provides);
            return graph;
        }

        /// <summary>
        /// Mutable state shared by the recursive walk. A private container keeps
        /// the public resolver signature small while still passing a single
        /// argument through the recursion.
        /// </summary>
        private sealed class ResolutionContext
        {
            public ModCollectionGraph Graph;
            public Dictionary<string, ModCollectionNode> Nodes;
            public HashSet<(string From, string To)> Edges;
            public HashSet<string> Provides;
            public List<ModCollection> Resolved;
            public IReadOnlyDictionary<string, ModCollection> Collections;
            public IReadOnlyDictionary<string, IModInfo> LocalMods;
            public IReadOnlyCollection<string> DisabledIds;
            public IReadOnlyCollection<string> SessionInstalled;
            public IReadOnlyDictionary<string, CatalogExportEntry> CatalogById;

            /// <summary>Locale used for localized group names/descriptions; null keeps flat fields.</summary>
            public string Language;

            /// <summary>
            /// Optional display-text translator (resolves '@key' references);
            /// null keeps raw values so headless tests stay deterministic.
            /// </summary>
            public Func<string, string> Translator;
        }

        /// <summary>
        /// Expands <see cref="ModCollection.Includes"/> of a parent into groups
        /// and flattens each child's subtree into its group. Role conflicts are
        /// checked per parent; cycles are detected through
        /// <paramref name="stack"/> so a malformed pair of documents degrades
        /// into a problem instead of a stack overflow.
        /// </summary>
        private static void ResolveIncludes(ModCollection parent, ResolutionContext context, List<string> stack)
        {
            var includes = parent.Includes ?? new List<ModCollectionInclude>();
            var roleOwners = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);

            foreach (var include in includes)
            {
                if (include == null || string.IsNullOrWhiteSpace(include.Ref))
                    continue;

                var dependency = ModDependency.Parse(include.Ref);
                var childId = dependency.IsValid ? dependency.ModId : include.Ref.Trim();
                var groupKey = $"i:{parent.Id}:{include.Ref}";

                if (childId == parent.Id || stack.Contains(childId, StringComparer.OrdinalIgnoreCase))
                {
                    context.Graph.Problems.Add(new ModCollectionProblem
                    {
                        Kind = ModCollectionProblemKind.Cycle,
                        Title = $"Cycle: '{parent.Name ?? parent.Id}' -> '{include.Ref}'",
                        Detail = "The include chain references a collection that is already being expanded.",
                        CollectionId = parent.Id,
                        ParentCollectionId = parent.Id,
                        Ref = include.Ref,
                        GroupKey = groupKey,
                    });
                    continue;
                }

                if (!TryGetCollection(context.Collections, childId, out var child))
                {
                    context.Graph.Problems.Add(new ModCollectionProblem
                    {
                        Kind = ModCollectionProblemKind.MissingCollection,
                        Title = $"Block '{childId}' is not available",
                        Detail = $"Required by slot '{include.Role}' of '{parent.Name ?? parent.Id}'. No mod or catalog provides it.",
                        CollectionId = parent.Id,
                        ParentCollectionId = parent.Id,
                        Ref = include.Ref,
                        GroupKey = groupKey,
                    });
                    continue;
                }

                RegisterRole(parent, include, child, roleOwners, groupKey, context);

                var group = new ModCollectionGroup
                {
                    Key = groupKey,
                    IsInclude = true,
                    CollectionId = child.Id,
                    ParentCollectionId = parent.Id,
                    Name = Translate(
                        string.IsNullOrEmpty(include.Label) ? child.GetLocalizedName(context.Language) : include.Label,
                        context.Translator),
                    Description = Translate(
                        string.IsNullOrEmpty(include.Note) ? child.GetLocalizedDescription(context.Language) : include.Note,
                        context.Translator),
                    Role = include.Role,
                    Optional = include.Optional,
                    Selected = include.IsSelected,
                };
                context.Graph.Groups.Add(group);

                if (!group.Selected)
                    continue;

                FlattenCollection(child, group, context, stack);
            }
        }

        /// <summary>
        /// Adds one included collection's mods and nested includes to the given
        /// group, deduplicating by mod id and registering provides for the
        /// requirement check.
        /// </summary>
        private static void FlattenCollection(ModCollection collection, ModCollectionGroup group, ResolutionContext context, List<string> stack)
        {
            AddProvides(collection, context.Provides);
            if (!context.Resolved.Any(c => string.Equals(c.Id, collection.Id, StringComparison.OrdinalIgnoreCase)))
                context.Resolved.Add(collection);

            var nextStack = new List<string>(stack) { collection.Id };

            foreach (var mechanic in collection.Mechanics ?? new List<ModCollectionMechanic>())
            {
                if (mechanic == null || !mechanic.IsSelected)
                    continue;

                if (string.IsNullOrEmpty(mechanic.Id))
                    mechanic.Id = $"mechanic_{mechanic.Mods?.Count ?? 0}";

                foreach (var raw in mechanic.Mods ?? new List<string>())
                    AddDeclaredMod(raw, collection.Id, mechanic, group, context);
            }

            foreach (var include in collection.Includes ?? new List<ModCollectionInclude>())
            {
                if (include == null || string.IsNullOrWhiteSpace(include.Ref) || !include.IsSelected)
                    continue;

                var dependency = ModDependency.Parse(include.Ref);
                var childId = dependency.IsValid ? dependency.ModId : include.Ref.Trim();

                if (childId == collection.Id || nextStack.Contains(childId, StringComparer.OrdinalIgnoreCase))
                {
                    context.Graph.Problems.Add(new ModCollectionProblem
                    {
                        Kind = ModCollectionProblemKind.Cycle,
                        Title = $"Cycle: '{collection.Name ?? collection.Id}' -> '{include.Ref}'",
                        Detail = "Nested include references a collection that is already being expanded.",
                        CollectionId = collection.Id,
                        ParentCollectionId = collection.Id,
                        Ref = include.Ref,
                    });
                    continue;
                }

                if (TryGetCollection(context.Collections, childId, out var child))
                    FlattenCollection(child, group, context, nextStack);
            }
        }

        /// <summary>
        /// Applies the optional translator to a display string. Without a
        /// translator (tests, headless tools) raw '@key' values stay verbatim;
        /// a translator returning null (missing translation) also keeps the
        /// raw value so group text never disappears.
        /// </summary>
        private static string Translate(string raw, Func<string, string> translator)
        {
            if (raw == null || translator == null)
                return raw;

            return translator(raw) ?? raw;
        }

        /// <summary>
        /// Records the role owner of a required include and reports a conflict
        /// when two required blocks claim the same slot. Optional blocks never
        /// conflict: the player may keep alternatives side by side until one is
        /// switched on.
        /// </summary>
        private static void RegisterRole(
            ModCollection parent,
            ModCollectionInclude include,
            ModCollection child,
            Dictionary<string, ModCollection> roleOwners,
            string groupKey,
            ResolutionContext context)
        {
            if (include.Optional || string.IsNullOrEmpty(include.Role))
                return;

            if (roleOwners.TryGetValue(include.Role, out var owner))
            {
                context.Graph.Problems.Add(new ModCollectionProblem
                {
                    Kind = ModCollectionProblemKind.RoleConflict,
                    Title = $"Slot '{include.Role}' is claimed twice",
                    Detail = $"'{owner.Name ?? owner.Id}' and '{child.Name ?? child.Id}' are both required in '{parent.Name ?? parent.Id}'.",
                    CollectionId = parent.Id,
                    ParentCollectionId = parent.Id,
                    Ref = include.Ref,
                    GroupKey = groupKey,
                });
                return;
            }

            roleOwners[include.Role] = child;
        }

        /// <summary>
        /// Creates the node for a mod declared by a mechanic and appends it to
        /// the owning group. The first declaration owns the node (and its
        /// mechanic id) while later groups still list the mod, so removing it
        /// from the right mechanic stays possible from the scheme editor.
        /// </summary>
        private static void AddDeclaredMod(
            string raw,
            string collectionId,
            ModCollectionMechanic mechanic,
            ModCollectionGroup group,
            ResolutionContext context)
        {
            var dependency = ModDependency.Parse(raw);
            var modId = dependency.IsValid ? dependency.ModId : raw?.Trim();
            if (string.IsNullOrEmpty(modId))
                return;

            if (group.Mods.Exists(r => string.Equals(r.ModId, modId, StringComparison.OrdinalIgnoreCase)))
                return;

            group.Mods.Add(new ModCollectionModRef
            {
                ModId = modId,
                Role = mechanic.Role,
                CollectionId = collectionId,
                MechanicId = mechanic.Id,
            });

            if (context.Nodes.TryGetValue(modId, out var existing))
            {
                if (existing.IsExternal)
                {
                    existing.MechanicId = mechanic.Id;
                    existing.IsExternal = false;
                }
                return;
            }

            var node = CreateNode(modId, mechanic.Id, dependency, false,
                context.LocalMods, context.DisabledIds, context.SessionInstalled, context.CatalogById);
            context.Nodes[modId] = node;
            context.Graph.Nodes.Add(node);
        }

        private static void AddProvides(ModCollection collection, HashSet<string> provides)
        {
            foreach (var capability in collection.Provides ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(capability))
                    provides.Add(capability.Trim());
            }
        }

        /// <summary>
        /// Reports collections whose <c>requires</c> are not satisfied by the
        /// union of <c>provides</c> of every resolved block.
        /// </summary>
        private static void CheckRequirements(ModCollectionGraph graph, List<ModCollection> resolved, HashSet<string> provides)
        {
            foreach (var collection in resolved)
            {
                foreach (var requirement in collection.Requires ?? new List<string>())
                {
                    var capability = ParseCapability(requirement);
                    if (string.IsNullOrEmpty(capability) || provides.Contains(capability))
                        continue;

                    graph.Problems.Add(new ModCollectionProblem
                    {
                        Kind = ModCollectionProblemKind.RequirementMissing,
                        Title = $"'{collection.Name ?? collection.Id}' requires '{capability}'",
                        Detail = "No resolved block provides this capability. Add a compatible block to the scheme.",
                        CollectionId = collection.Id,
                    });
                }
            }
        }

        private static string ParseCapability(string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement))
                return null;

            var dependency = ModDependency.Parse(requirement);
            return dependency.IsValid ? dependency.ModId : requirement.Trim();
        }

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

                var dependencies = modInfo.Manifest?.Depends;
                if (dependencies == null)
                    continue;

                foreach (var dependency in dependencies)
                {
                    var dependencyId = dependency.ModId;
                    if (!dependency.IsValid || string.IsNullOrEmpty(dependencyId))
                        continue;
                    // An optional dependency that is not installed is no
                    // requirement: it must not show up as a missing mod.
                    if (dependency.IsOptional && !localMods.ContainsKey(dependencyId))
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
