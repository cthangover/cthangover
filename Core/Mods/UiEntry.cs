using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for one entry of a mod manifest's <c>ui</c> array — the single,
    /// manifest-driven way a mod declares a GUI element (no C# registration API).
    /// <para/>
    /// Two kinds exist. <see cref="KindPanel"/> is a button-toggled panel: the UI
    /// registry owns a toolbar button for it and is the only one allowed to change
    /// its visibility. <see cref="KindOverlay"/> has no button; the element's own
    /// code decides when it is visible (optionally auto-shown by
    /// <see cref="ShowOnStart"/>).
    /// <para/>
    /// Entries are validated at startup by <c>UiRegistry.Initialize</c>; invalid
    /// entries are logged and skipped. <see cref="ModId"/> and
    /// <see cref="ModOrderIndex"/> are filled in during collection and never come
    /// from JSON — they carry the owning mod and its load-order position, which are
    /// needed to break ordering ties deterministically.
    /// </summary>
    public class UiEntry
    {
        /// <summary>Panel kind: has a toolbar button, visibility owned by the registry.</summary>
        public const string KindPanel = "panel";

        /// <summary>Overlay kind: no button, visibility owned by the element's own code.</summary>
        public const string KindOverlay = "overlay";

        /// <summary>Unique within the owning mod; doubles as the runtime cache key and Toggle target.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Either <see cref="KindPanel"/> or <see cref="KindOverlay"/>.</summary>
        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        /// <summary>
        /// Optional logical scene this GUI belongs to. When set, the GUI is only
        /// available in that scene and is hidden when leaving it. When absent the
        /// GUI is scene-independent and lives for the whole session.
        /// </summary>
        [JsonPropertyName("scene")]
        public string Scene { get; set; }

        /// <summary>Name of the anchor slot node (e.g. "ModBeforeUIPanel", "Lastground").</summary>
        [JsonPropertyName("slot")]
        public string Slot { get; set; }

        /// <summary>Mod-relative path to the .tscn scene, resolved through TscnScenes.</summary>
        [JsonPropertyName("scene_path")]
        public string ScenePath { get; set; }

        /// <summary>
        /// When true the instance lives for the whole application; when false it is
        /// created on scene entry and freed on scene exit. Panels are always treated
        /// as <c>true</c> during collection.
        /// </summary>
        [JsonPropertyName("once")]
        public bool Once { get; set; }

        /// <summary>
        /// Overlays only: when true the element is shown as soon as its scene is
        /// entered; when false (default) it waits for a registry Toggle or its own
        /// code. Ignored for panels.
        /// </summary>
        [JsonPropertyName("show_on_start")]
        public bool ShowOnStart { get; set; }

        /// <summary>Layer/order inside the anchor slot. Defaults to 10; optional.</summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 10;

        /// <summary>Present only for <see cref="KindPanel"/>; forbidden for overlays.</summary>
        [JsonPropertyName("button")]
        public UiButtonEntry Button { get; set; }

        /// <summary>Owning mod id, filled during collection.</summary>
        [JsonIgnore]
        public string ModId { get; set; }

        /// <summary>Position of the owning mod in load order, filled during collection.</summary>
        [JsonIgnore]
        public int ModOrderIndex { get; set; }
    }
}
