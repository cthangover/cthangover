using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Resolved, render-ready form of a <see cref="ModCollection"/>: the mods it
    /// needs (including transitive dependencies), the dependency edges between
    /// them, and each mod's availability state. Built by
    /// <see cref="ModCollectionResolver"/>; consumed by the collections screen and
    /// its <c>GraphEdit</c> view, and by <see cref="ModCollectionPlan"/> when the
    /// player applies the collection.
    /// <para/>
    /// The graph is intentionally not persisted: it is always derived from the
    /// live registry + catalog cache, so it cannot go stale after installs or
    /// catalog refreshes.
    /// </summary>
    public class ModCollectionGraph
    {
        /// <summary>Mechanics in declaration order; ids normalized to non-empty values by the resolver.</summary>
        public List<ModCollectionMechanic> Mechanics { get; set; } = new();

        /// <summary>
        /// Scheme contours in rendering order: root includes first, then the
        /// root's own mechanics. Each group carries its display name,
        /// description and the mods flattened from its subtree, so the editor
        /// can draw the game as a two-level pyramid without walking include
        /// chains itself.
        /// </summary>
        public List<ModCollectionGroup> Groups { get; set; } = new();

        /// <summary>
        /// Resolution inconsistencies (missing blocks, cycles, role conflicts,
        /// unmet requirements). Empty for a consistent assembly; the editor
        /// blocks applying while errors are present.
        /// </summary>
        public List<ModCollectionProblem> Problems { get; set; } = new();

        /// <summary>All mod vertices: mechanic members first, then external dependencies in discovery order.</summary>
        public List<ModCollectionNode> Nodes { get; set; } = new();

        /// <summary>Dependency edges; <c>From</c> is the dependency of <c>To</c>.</summary>
        public List<ModCollectionEdge> Edges { get; set; } = new();

        /// <summary>Number of nodes already installed and enabled with a satisfying version.</summary>
        public int InstalledCount => CountByState(ModCollectionNodeState.Installed);

        /// <summary>Total number of mods the collection needs, external dependencies included.</summary>
        public int TotalCount => Nodes.Count;

        private int CountByState(ModCollectionNodeState state)
        {
            var count = 0;
            foreach (var node in Nodes)
            {
                if (node.State == state)
                    count++;
            }

            return count;
        }
    }
}
