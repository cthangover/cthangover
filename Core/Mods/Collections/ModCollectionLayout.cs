using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Free-form arrangement of one scheme: where each group sits and how it is
    /// sized, plus where inside it every mod chip was placed. Purely a UI
    /// concern — the collection documents never store it — so it lives in
    /// <c>user://collection_layout.json</c> through
    /// <see cref="CollectionLayoutStore"/> and is keyed by game id.
    /// <para/>
    /// Group keys match <see cref="ModCollectionGroup.Key"/>; mod keys are
    /// <c>"&lt;groupKey&gt;|&lt;modId&gt;"</c>. Entries for groups or mods that no
    /// longer exist are harmless and simply ignored, which keeps re-adding a
    /// removed mechanic to its old spot.
    /// </summary>
    public class ModCollectionLayout
    {
        /// <summary>Group key -> frame rectangle.</summary>
        [JsonPropertyName("groups")]
        public Dictionary<string, ModCollectionLayoutRect> Groups { get; set; } = new();

        /// <summary>"groupKey|modId" -> chip position inside the group.</summary>
        [JsonPropertyName("mods")]
        public Dictionary<string, ModCollectionLayoutPoint> Mods { get; set; } = new();
    }
}
