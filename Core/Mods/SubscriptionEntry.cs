using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for a single entry in <c>manifest.json</c>'s
    /// <c>subscriptions</c> array. Each entry binds a UI wrapper
    /// template to a scene lifecycle trigger — when the specified scene
    /// fires the trigger event, the wrapped UI is instantiated and the
    /// optional <c>Code</c> snippet is executed to configure it. This is
    /// the primary mechanism by which mods inject custom panels,
    /// overlays, and HUD elements without editing core scene files.
    /// </summary>
    public class SubscriptionEntry
    {
        /// <summary>
        /// Scene name this subscription targets (matches
        /// <c>SceneDefinition.Name</c>). The wrapper is instantiated
        /// when this scene becomes active.
        /// </summary>
        [JsonPropertyName("scene")]
        public string Scene { get; set; }

        /// <summary>Name of the <c>.wrappertmpl</c> file (minus extension) to instantiate.</summary>
        [JsonPropertyName("template")]
        public string Template { get; set; }

        /// <summary>
        /// Z-ordering within the scene's subscription list. Higher
        /// values render on top. Defaults to 10 so there is room
        /// below for background-layer wrappers.
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 10;

        /// <summary>
        /// Lifecycle trigger that activates this subscription
        /// ("on_enter", "on_ready", etc.). Defaults to "on_enter"
        /// so wrappers appear as soon as the scene loads.
        /// </summary>
        [JsonPropertyName("trigger")]
        public string Trigger { get; set; } = "on_enter";

        /// <summary>
        /// Optional C# code executed when the trigger fires. Serves
        /// as a lightweight alternative to a full compiled mod for
        /// simple UI tweaks.
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }
    }
}
