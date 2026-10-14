using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Map
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
    /// <c>mods/&lt;name&gt;/map/</c>. Zones are read per mod in load order, so later
    /// mods can override zones by <see cref="ID"/>.
    /// <para/>
    /// This type is raw authoring data only. The live open/hidden state of a
    /// zone is owned by <see cref="MapZoneService"/>, which seeds itself from
    /// <see cref="Locked"/>/<see cref="Hidden"/> and lets scenarios and code
    /// change it at runtime; only those runtime overrides are saved.
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
        /// An empty or unresolved scene marks the zone as closed: the map shows
        /// its marker and polygon dimmed and blocks the transition button, which
        /// lets authors sketch future regions without breaking navigation.
        /// </summary>
        [JsonPropertyName("scene")]
        public string Scene { get; set; }

        /// <summary>
        /// Optional localisation key for the flavour text shown in the location
        /// card. Zones without a description show only the state badge and the
        /// transition button, so map data can stay minimal.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

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

        /// <summary>
        /// Normalised [0..1] position of a free-standing text label drawn
        /// directly on the map. <c>null</c> (absent in JSON) means the zone has
        /// no label. A zone with a label but no <see cref="Image"/> is a pure
        /// inscription — it is not a clickable location, does not open the
        /// location card and is excluded from the zone counter. Used for
        /// region names that have no icon of their own.
        /// </summary>
        [JsonPropertyName("label_pos")]
        [JsonConverter(typeof(Vector2NullableJsonConverter))]
        public Vector2? LabelPos { get; set; }

        /// <summary>
        /// Authoring default for the zone being closed to travel. <c>false</c>
        /// (the default) means the zone is enterable as soon as its scene
        /// resolves. Runtime state is owned by <see cref="MapZoneService"/>:
        /// scenarios open and close zones through the <c>map.open_zone</c> /
        /// <c>map.close_zone</c> actions, and those overrides are persisted in
        /// the save, while this field only seeds the state of an untouched zone.
        /// </summary>
        [JsonPropertyName("locked")]
        public bool Locked { get; set; }

        /// <summary>
        /// Authoring default for the zone being completely hidden from the
        /// map. <c>false</c> (the default) draws the zone. Hidden zones are
        /// absent from the map until a scenario reveals them with the
        /// <c>map.show_zone</c> action; like <see cref="Locked"/>, this is only
        /// the initial value and the runtime override lives in
        /// <see cref="MapZoneService"/>.
        /// </summary>
        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }
    }
}
