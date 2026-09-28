namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Availability of one mod in a <see cref="ModCollectionGraph"/> node,
    /// computed by <see cref="ModCollectionResolver"/> from the local registry,
    /// the disabled store and the merged online catalog. Drives node colors in
    /// the graph view and the install/enable decisions of
    /// <see cref="ModCollectionPlan"/>.
    /// </summary>
    public enum ModCollectionNodeState
    {
        /// <summary>Installed locally, enabled, and (when the collection pins a version) satisfies the pin.</summary>
        Installed,

        /// <summary>Installed locally but listed in <c>disabled_mods.json</c>.</summary>
        Disabled,

        /// <summary>Not installed, but available for download in a catalog.</summary>
        InCatalog,

        /// <summary>Not installed and unknown to every catalog — the plan cannot fix it.</summary>
        Missing,

        /// <summary>Installed, but its version does not satisfy the version pinned by the collection.</summary>
        VersionMismatch,
    }
}
