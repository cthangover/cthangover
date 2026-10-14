namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Kinds of inconsistency <see cref="ModCollectionResolver"/> can find while
    /// expanding a collection pyramid. The scheme editor groups them by severity
    /// (errors block applying, warnings do not) and offers "show" navigation to
    /// the offending group or mod.
    /// </summary>
    public enum ModCollectionProblemKind
    {
        /// <summary>An <c>includes[]</c> reference points at a collection nobody provides.</summary>
        MissingCollection,

        /// <summary>Include chain references a collection already on the active stack.</summary>
        Cycle,

        /// <summary>Two required includes claim the same role slot.</summary>
        RoleConflict,

        /// <summary>A resolved collection requires a capability no block provides.</summary>
        RequirementMissing,
    }
}
