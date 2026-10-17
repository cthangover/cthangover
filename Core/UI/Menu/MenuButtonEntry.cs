using System;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// One menu item in the <see cref="MenuRegistry"/>: a button shown in a menu
    /// block and the thing it is bound to. A button is bound either to a
    /// mod-relative scene path (pressed → the scene opens as a fullscreen child
    /// of the menu root, the declarative manifest behaviour) or to a
    /// <see cref="Handler"/> delegate (pressed → mod code runs, e.g. the built-in
    /// new game / exit actions).
    /// <para/>
    /// Entries arrive from two sources and live in one ordered list:
    /// manifest <c>main_menu</c> declarations (collected by
    /// <see cref="MenuRegistry.Rebuild"/>, <c>Section</c> fixed to
    /// <see cref="SectionMods"/>) and runtime registrations from mod initializers
    /// via <see cref="MenuRegistry.RegisterButton"/>. <see cref="ModOrderIndex"/>
    /// orders manifest entries by mod load position; runtime entries use
    /// <c>-1</c> so equal-index registrations keep their programmatic priority.
    /// </summary>
    public class MenuButtonEntry
    {
        /// <summary>Buttons whose <see cref="Section"/> is not provided land in the mod block.</summary>
        public const string SectionMods = "mods";

        /// <summary>Unique button id, namespaced by convention to the owning mod ("my_mod/my_button").</summary>
        public string Id { get; set; }

        /// <summary>Id of the registering mod, filled by the registry.</summary>
        public string ModId { get; set; }

        /// <summary>
        /// Menu block the button belongs to. The host menu maps section names to
        /// containers; unknown sections fall back to <see cref="SectionMods"/>.
        /// </summary>
        public string Section { get; set; } = SectionMods;

        /// <summary>Translation key for the caption (required — the button must be localizable).</summary>
        public string Locale { get; set; }

        /// <summary>Optional icon key resolved through UIIconFactory.</summary>
        public string Icon { get; set; }

        /// <summary>Optional theme type variation suffix ("primary", "danger"); applied as "Cthangover&lt;Suffix&gt;".</summary>
        public string Variation { get; set; }

        /// <summary>Vertical order inside the section; smaller is higher.</summary>
        public int Index { get; set; } = 10;

        /// <summary>
        /// Optional visibility predicate evaluated each time the menu is built
        /// (e.g. "Continue" only when a last session exists); null = always.
        /// </summary>
        public System.Func<bool> Visible { get; set; }

        /// <summary>
        /// Mod-relative scene path opened on press. Optional when
        /// <see cref="Handler"/> is set; at least one of the two is required.
        /// </summary>
        public string ScenePath { get; set; }

        /// <summary>
        /// Press callback receiving the host menu root. Optional when
        /// <see cref="ScenePath"/> is set; at least one of the two is required.
        /// </summary>
        public Action<Control> Handler { get; set; }

        /// <summary>Position of the owning mod in load order; <c>-1</c> for runtime registrations until resolved.</summary>
        public int ModOrderIndex { get; set; } = -1;

        /// <summary>True when the button has a localizable caption and a press target.</summary>
        internal bool IsValid => !string.IsNullOrWhiteSpace(Id)
                                 && !string.IsNullOrWhiteSpace(Locale)
                                 && (!string.IsNullOrWhiteSpace(ScenePath) || Handler != null);
    }
}
