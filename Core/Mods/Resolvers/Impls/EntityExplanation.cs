namespace Cthangover.Core.Mods.Resolvers
{
    using System.Collections.Generic;
    using System.Text.Json.Nodes;

    /// <summary>
    /// The resolved value of one entity with the origin of each field, produced by
    /// <see cref="EntityResolver.Explain"/> for editors: they show what the game sees and who set
    /// it, and write changes to the right place. A leaf missing from <see cref="Fields"/> is the
    /// DTO default (nobody wrote it).
    /// </summary>
    public sealed class EntityExplanation
    {
        /// <summary>Entity kind.</summary>
        public string Kind { get; init; }

        /// <summary>Entity id.</summary>
        public string Id { get; init; }

        /// <summary>The resolved entity as the game reads it (DTO serialised); null when deleted.</summary>
        public JsonObject Value { get; init; }

        /// <summary>True when a patch removed the entity with <c>"$delete": true</c>.</summary>
        public bool Deleted { get; init; }

        /// <summary>Origins by leaf path (case-insensitive).</summary>
        public IReadOnlyDictionary<string, EntityFieldOrigin> Fields { get; init; }
    }
}
