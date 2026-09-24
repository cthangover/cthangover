using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for the <c>button</c> object of a <c>kind="panel"</c> entry in
    /// a mod manifest's <c>ui</c> array. The toolbar button is owned by the UI
    /// registry, not by the mod: the registry resolves <see cref="Icon"/> through
    /// UIIconFactory (mod-aware, unlike Godot's ResourceLoader), labels it with the
    /// translated <see cref="Locale"/> key, and orders it by <see cref="Index"/>.
    /// Several buttons may share an index; ties are broken by mod load order.
    /// </summary>
    public class UiButtonEntry
    {
        /// <summary>Logical icon id resolved through UIIconFactory. Required for panels.</summary>
        [JsonPropertyName("icon")]
        public string Icon { get; set; }

        /// <summary>Translation key for the button tooltip. Required for panels.</summary>
        [JsonPropertyName("locale")]
        public string Locale { get; set; }

        /// <summary>
        /// Toolbar order index. Lower values go first. Defaults to 10 so mods
        /// that do not care keep a stable, load-order-based position.
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; } = 10;
    }
}
