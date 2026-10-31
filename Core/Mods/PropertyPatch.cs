using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One JSON property patch targeting an existing entity: the <c>ID</c>
    /// names the entity and every entry of <c>ExtraProperties</c> overrides or
    /// extends one property. Collected by <see cref="Resolvers.PatchResolver"/>
    /// from <c>patches/&lt;kind&gt;/</c> directories (and flat
    /// <c>patches/&lt;kind&gt;.json</c> files) and merged into the base entity
    /// by <see cref="Resolvers.EntityResolver"/>: wildcard (<c>*</c>) patches
    /// first, then the addressed ones (id compared case-insensitively), each in
    /// load order and each applied and checked on its own.
    /// </summary>
    public class PropertyPatch : IIdentifiable
    {
        /// <summary>ID of the entity this patch modifies.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>
        /// The patch body keyed by property name, merged by <see cref="Patches.JsonPatchMerger"/>
        /// (objects deep-merge, <c>null</c> removes, <c>Key$add/$remove/$set</c> on arrays). The
        /// key <c>"$delete": true</c> removes the whole entity instead.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> ExtraProperties { get; set; }

        /// <summary>Mod that shipped the patch (set by the resolver, not read from JSON); used in error reports.</summary>
        [JsonIgnore]
        public string ModId { get; set; }

        /// <summary>Mod-relative file of the patch (set by the resolver); used in error reports.</summary>
        [JsonIgnore]
        public string File { get; set; }
    }
}
