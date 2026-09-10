using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Describes one interactive zone on the world map. Defines its bounding
    /// polygon (for a semi-transparent highlight), a clickable marker icon,
    /// a localised title label, and the target scene that opens when clicked.
    ///
    /// All coordinate properties (<see cref="Points"/> and <see cref="ImagePos"/>)
    /// use normalised values in the [0.0 .. 1.0] range, relative to the map
    /// background image dimensions. This makes zone layouts resolution-independent
    /// and safe to share across mods.
    ///
    /// Maps use the standard <c>{"Items": [...]}</c> JSON envelope in
    /// <c>mods/&lt;name&gt;/map/</c>, compatible with
    /// <c>ModManager.CollectJsonGroup&lt;MapZone&gt;("map")</c> so that later
    /// mods can override zones by <see cref="ID"/>.
    /// </summary>
    public class MapZone : IIdentifiable
    {
        /// <summary>
        /// Unique zone identifier used as the dictionary key in factory collections.
        /// Typically equals the target scene name for clarity.
        /// </summary>
        [JsonPropertyName("ID")]
        public string ID { get; set; }

        /// <summary>
        /// Polygon vertices in normalised map coordinates [0..1].
        /// Drawn as a semi-transparent colour wash over the map background
        /// to visually outline the zone.
        /// </summary>
        [JsonPropertyName("points")]
        [JsonConverter(typeof(Vector2ListJsonConverter))]
        public List<Vector2> Points { get; set; } = new();

        /// <summary>
        /// Localisation key for the zone name displayed next to the marker.
        /// Resolved via <c>TranslationServer.Translate</c>.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Target scene identifier passed directly to
        /// <c>SceneManager.SwitchScene</c> when the marker is clicked.
        /// Causes the current visual-novel scene to transition to this zone.
        /// </summary>
        [JsonPropertyName("scene")]
        public string Scene { get; set; }

        /// <summary>
        /// Asset ID of the marker icon texture, resolved through the mod texture
        /// system (e.g. <c>ModManager.ResolveTexture</c>).
        /// </summary>
        [JsonPropertyName("image")]
        public string Image { get; set; }

        /// <summary>
        /// Normalised [0..1] position of the marker icon on the map image.
        /// Stored as <c>[x, y]</c> in JSON, deserialized into a <see cref="Vector2"/>.
        /// </summary>
        [JsonPropertyName("image_pos")]
        [JsonConverter(typeof(Vector2JsonConverter))]
        public Vector2 ImagePos { get; set; }
    }
}
