namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// A reusable overlay panel in the <see cref="MenuRegistry"/> — settings,
    /// save/load and any mod-provided screen that other menus open by id instead
    /// of by type. The panel content is a mod-relative scene instantiated on
    /// demand; the panel shows and hides itself, so the registry only needs the
    /// scene path and a stable id.
    /// <para/>
    /// Panels are registered at runtime by mod initializers
    /// (<see cref="MenuRegistry.RegisterPanel"/>); the host menu then opens them
    /// through <see cref="MenuRegistry.InstantiatePanel"/> without a compile-time
    /// dependency on the mod's classes. Panels that want to expose typed
    /// behaviour to core menus implement the small interfaces from
    /// <see cref="IMenuPanel"/> on their root script.
    /// </summary>
    public class MenuPanelEntry
    {
        /// <summary>Unique panel id ("settings", "save_load", "my_mod/my_panel").</summary>
        public string Id { get; set; }

        /// <summary>Id of the registering mod, filled by the registry.</summary>
        public string ModId { get; set; }

        /// <summary>Mod-relative path to the .tscn instantiated when the panel opens (required).</summary>
        public string ScenePath { get; set; }

        /// <summary>Optional translation key for the panel title, for menus that render captions.</summary>
        public string Locale { get; set; }

        /// <summary>True when the panel has an id and a scene to instantiate.</summary>
        internal bool IsValid => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(ScenePath);
    }
}
