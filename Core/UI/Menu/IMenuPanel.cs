using System;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Shared panel contracts core menus consume through the
    /// <see cref="MenuRegistry"/> without a compile-time dependency on the mod
    /// classes behind them. A panel scene registers its
    /// <see cref="MenuPanelEntry"/> in an IModInitializer and its root script
    /// implements the matching interface; the host menu instantiates the panel
    /// via <see cref="MenuRegistry.InstantiatePanel"/> and talks to the
    /// interface only.
    /// </summary>
    public interface IMenuPanel
    {
        /// <summary>Makes the panel visible on top of its host menu.</summary>
        void Open();
    }

    /// <summary>
    /// Settings panels opened from other menus. Host menus subscribe to
    /// <see cref="LanguageChanged"/> to retranslate their own buttons after a
    /// language switch inside the panel.
    /// </summary>
    public interface ISettingsPanel : IMenuPanel
    {
        /// <summary>Raised after the player applied a different language.</summary>
        event Action LanguageChanged;
    }

    /// <summary>Save/load panels with their two entry modes.</summary>
    public interface ISaveLoadPanel : IMenuPanel
    {
        /// <summary>Opens the panel in load mode and refreshes the slot list.</summary>
        void OpenForLoad();

        /// <summary>Opens the panel in save mode and refreshes the slot list.</summary>
        void OpenForSave();
    }
}
