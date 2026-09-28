namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Directed edge of a <see cref="ModCollectionGraph"/>: <see cref="FromModId"/>
    /// is the dependency, <see cref="ToModId"/> is the mod that depends on it.
    /// The direction matches load order — dependencies are enabled and compiled
    /// before their dependents — so the graph view can draw arrows pointing from
    /// the foundation towards the mods built on top of it.
    /// </summary>
    public class ModCollectionEdge
    {
        /// <summary>Id of the dependency mod.</summary>
        public string FromModId { get; set; }

        /// <summary>Id of the mod that declares <see cref="FromModId"/> in its <c>depends</c>.</summary>
        public string ToModId { get; set; }
    }
}
