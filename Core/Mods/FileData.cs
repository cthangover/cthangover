using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Generic JSON wrapper for mod data files. Every entity JSON is a
    /// self-describing envelope: a top-level <c>"Kind"</c> string names the
    /// entity type (resolved through <see cref="EntityKindRegistry"/>) and
    /// <c>"Items"</c> carries the typed entries. The kind is what makes a file
    /// locatable regardless of which directory it lives in — the entity
    /// resolver scans all mod files and routes each one by its kind instead of
    /// assuming that a folder means a type.
    /// <para/>
    /// Files without a kind are not entity data (manifests, collections,
    /// scene definitions, patches) and are never parsed through this type.
    /// </summary>
    [Serializable]
    public class FileData<T> where T : class, IIdentifiable
    {
        /// <summary>
        /// Canonical entity kind of this file, e.g. <c>"skill"</c>. Must match
        /// the <see cref="EntityKindAttribute"/> of <typeparamref name="T"/>;
        /// the entity resolver skips a file whose declared kind does not match
        /// the DTO it is being collected for.
        /// </summary>
        [JsonPropertyName("Kind")]
        public string Kind { get; set; }

        /// <summary>The typed entries inside the {"Items": [...]} JSON envelope.</summary>
        [JsonPropertyName("Items")]
        public List<T> Items { get; set; }
    }
}
