using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for one entry of a mod manifest's <c>main_menu</c> array — the
    /// declarative way a mod adds a button to the title screen and binds a scene
    /// to it. This is the main-menu counterpart of <see cref="UiEntry"/>: same
    /// manifest-driven philosophy, but a different surface. Main menu GUI lives
    /// in the native <c>main_menu.tscn</c> (which mods cannot patch), is built by
    /// <c>MenuRegistry</c> + <c>MenuBuilder</c> and is never hosted by
    /// <c>UiHost</c>.
    /// <para/>
    /// The target scene is a mod-relative <c>.tscn</c> resolved through
    /// <see cref="Scenes.ModScenePrefab"/>. It is added as a full-rect child of
    /// the main menu root, so it is a fullscreen overlay owned by the mod itself:
    /// it closes by freeing itself (or by another mod button replacing it).
    /// <see cref="Button"/> reuses <see cref="UiButtonEntry"/> for ordering and
    /// the translation key; its icon is optional here (plain menu buttons are
    /// text-only unless the mod ships an icon), unlike panel buttons in
    /// <see cref="UiEntry"/> where the icon is mandatory.
    /// </summary>
    public class MainMenuEntry
    {
        /// <summary>Unique within the owning mod; used for validation and registry lookup.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Mod-relative path to the .tscn implementing the button's screen (fullscreen Control).</summary>
        [JsonPropertyName("scene_path")]
        public string ScenePath { get; set; }

        /// <summary>Button definition: <c>locale</c> is required, <c>icon</c> optional, <c>index</c> sets the vertical order.</summary>
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
