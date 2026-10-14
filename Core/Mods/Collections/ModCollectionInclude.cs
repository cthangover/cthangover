using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Reference from a parent <see cref="ModCollection"/> to a child collection
    /// inside <see cref="ModCollection.Includes"/>. This is the recursive edge of
    /// the collection pyramid: the parent declares which blocks (mechanics,
    /// systems, content) compose it, and the resolver expands them into groups
    /// of mods. <see cref="Ref"/> uses the same versioned syntax as
    /// <c>manifest.json</c> dependencies (<c>"rpg_2d&gt;=1"</c>), parsed with
    /// <see cref="ModDependency.Parse"/>.
    /// <para/>
    /// <see cref="Label"/> and <see cref="Note"/> are the scheme-editor overlays:
    /// the player may rename a block inside one game without touching the shared
    /// collection document, so a component can be called "2D RPG" in the catalog
    /// and "Основа" in a specific game. Null/empty values fall back to the child
    /// collection's own name and description.
    /// </summary>
    public class ModCollectionInclude
    {
        /// <summary>Versioned collection reference, e.g. <c>"rpg_2d&gt;=1"</c>.</summary>
        [JsonPropertyName("ref")]
        public string Ref { get; set; }

        /// <summary>Slot role inside the parent ("systems", "story", "ui", ...); drives conflict checks.</summary>
        [JsonPropertyName("role")]
        public string Role { get; set; }

        /// <summary>True when the block may be excluded without breaking the parent.</summary>
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }

        /// <summary>
        /// Player's choice for an optional block. <c>null</c> (omitted) means the
        /// default: optional blocks are on until the player turns them off, which
        /// keeps old documents and hand-written collections behaving as before.
        /// </summary>
        [JsonPropertyName("selected")]
        public bool? Selected { get; set; }

        /// <summary>Per-game name override; null falls back to the child collection name.</summary>
        [JsonPropertyName("label")]
        public string Label { get; set; }

        /// <summary>Per-game description override; null falls back to the child collection description.</summary>
        [JsonPropertyName("note")]
        public string Note { get; set; }

        /// <summary>True when the block participates in resolution (not an off optional).</summary>
        [JsonIgnore]
        public bool IsSelected => !(Optional && Selected == false);
    }
}
