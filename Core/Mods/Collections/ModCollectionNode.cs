namespace Cthangover.Core.Mods
{
    using System.Collections.Generic;
    /// <summary>
    /// One mod vertex of a <see cref="ModCollectionGraph"/>. Nodes exist both
    /// for the mods explicitly listed by the collection's mechanics and for
    /// their transitive dependencies pulled in by
    /// <see cref="ModCollectionResolver"/>; the latter carry
    /// <see cref="IsExternal"/> so the graph view can draw them in a separate
    /// "dependencies" cluster instead of mixing them into a mechanic.
    /// </summary>
    public class ModCollectionNode
    {
        /// <summary>Canonical mod id (matches <see cref="ModInfo.Id"/> and catalog ids).</summary>
        public string ModId { get; set; }

        /// <summary>Best available display name (local manifest or catalog), falling back to the id.</summary>
        public string Title { get; set; }

        /// <summary>Owning mechanic id, or null for external dependencies.</summary>
        public string MechanicId { get; set; }

        /// <summary>True when the node was pulled in as a dependency, not declared by a mechanic.</summary>
        public bool IsExternal { get; set; }

        /// <summary>
        /// Version constraint the collection (or a dependent mod) puts on this
        /// mod, parsed with <see cref="ModDependency.Parse"/>. An invalid default
        /// value means "any version".
        /// </summary>
        public ModDependency Dependency { get; set; }

        /// <summary>
        /// Every explicit and transitive requirement for this single mod id.
        /// Deduplicating a vertex must not discard another group's pin or a
        /// consumer's dependency; candidates have to satisfy their intersection.
        /// </summary>
        public List<ModDependency> Requirements { get; } = new();

        /// <summary>Installed version, or null when the mod is not present locally.</summary>
        public string InstalledVersion { get; set; }

        /// <summary>Derived availability state; see <see cref="ModCollectionNodeState"/>.</summary>
        public ModCollectionNodeState State { get; set; }

        /// <summary>Human-readable version constraint (operator + version), or null when unconstrained.</summary>
        public string RequiredVersion => Dependency.IsValid && !string.IsNullOrEmpty(Dependency.VersionRaw)
            ? Dependency.Operator + Dependency.VersionRaw
            : null;

        /// <summary>
        /// Checks all collected requirements. The original dependency remains
        /// a fallback for callers constructing a node directly without a resolver.
        /// </summary>
        public bool AcceptsVersion(string version)
        {
            if (Dependency.IsValid && !Dependency.IsSatisfiedBy(version))
                return false;

            foreach (var requirement in Requirements)
            {
                if (requirement.IsValid && !requirement.IsSatisfiedBy(version))
                    return false;
            }
            return true;
        }
    }
}
