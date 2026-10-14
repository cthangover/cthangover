using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One JSON property patch targeting an existing entity: the <c>ID</c>
    /// names the entity and every entry of <c>ExtraProperties</c> overrides or
    /// extends one property. Collected by <see cref="Resolvers.Impls.PatchResolver"/>
    /// from <c>patches/&lt;kind&gt;/</c> directories (and flat
    /// <c>patches/&lt;kind&gt;.json</c> files) and merged into the base entity
    /// by <see cref="Resolvers.Impls.EntityResolver"/>. Array-valued properties
    /// are concatenated across patches instead of overwritten, so several mods
    /// can extend the same list (e.g. extra skills).
    /// </summary>
    public class PropertyPatch : IIdentifiable
    {
        /// <summary>ID of the entity this patch modifies.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Property overrides keyed by property name; arrays concatenate, scalars replace.</summary>
        [JsonExtensionData]
        public Dictionary<string, object> ExtraProperties { get; set; }
    }
}
