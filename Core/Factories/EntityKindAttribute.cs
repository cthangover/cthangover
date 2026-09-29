using System;

namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Declares which entity kind a JSON-deserializable DTO describes. The
    /// kind is the sole type discriminator of the mod data pipeline: every
    /// data JSON carries a top-level <c>"Kind"</c> string in its
    /// <c>FileData&lt;T&gt;</c> envelope, and <see cref="EntityKindRegistry"/>
    /// resolves the kind to exactly one DTO type. This replaces the old
    /// convention where the directory a file lived in implied its type —
    /// a convention that broke down when several index files (characters,
    /// enemies, actions, skills) shared the <c>characters/</c> directory and
    /// every factory ended up parsing its neighbours' files.
    /// <para/>
    /// One kind maps to one DTO, and one JSON file declares one kind. Files
    /// that are not mod data (manifests, collections, live2d models, scene
    /// definitions) simply do not carry a kind and are never parsed by the
    /// entity resolver.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class EntityKindAttribute : Attribute
    {
        /// <summary>
        /// The canonical lowercase kind string (e.g. <c>"skill"</c>) that
        /// matches the <c>"Kind"</c> field written in the DTO's JSON files.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Creates the attribute. The kind is normalised to lowercase so
        /// that authoring case differences never split one entity type
        /// into two caches.
        /// </summary>
        /// <param name="kind">Canonical kind string, e.g. <c>"skill"</c>.</param>
        public EntityKindAttribute(string kind)
        {
            Kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim().ToLowerInvariant();
        }
    }
}
