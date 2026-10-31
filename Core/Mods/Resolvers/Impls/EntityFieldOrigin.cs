namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Where one field of a resolved entity comes from. A field inherited through <c>Extends</c>
    /// keeps the origin it had in the ancestor: <see cref="EntityId"/> then names the ancestor, so an
    /// editor can say "from parent X, set by mod Y's patch".
    /// </summary>
    public sealed class EntityFieldOrigin
    {
        /// <summary>Leaf path in the resolved JSON (<see cref="Patches.JsonLeafPaths"/> format).</summary>
        public string Path { get; init; }

        /// <summary>Kind of layer that set the value.</summary>
        public EntityLayer Layer { get; init; }

        /// <summary>The entity whose definition or patch set it: the explained id, or an <c>Extends</c> ancestor.</summary>
        public string EntityId { get; init; }

        /// <summary>Mod that shipped the layer.</summary>
        public string ModId { get; init; }

        /// <summary>Mod-relative file of the layer.</summary>
        public string File { get; init; }
    }
}
