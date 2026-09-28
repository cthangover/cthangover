using Cthangover.Core.Mods;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Locale keys and node colors for <see cref="ModCollectionNodeState"/>,
    /// following the same pattern as <see cref="ModStatusNames"/> so the
    /// collections graph reads like the rest of the manager: green — ready to
    /// play, amber — needs an action (enable, resolve a version pin), blue —
    /// downloadable from the catalog, red — impossible to resolve.
    /// </summary>
    public static class CollectionStateNames
    {
        /// <summary>Localized state label shown inside graph nodes and in the details panel.</summary>
        public static string Translate(ModCollectionNodeState state)
            => TranslationServer.Translate(Key(state));

        /// <summary>Translation key of a state.</summary>
        public static string Key(ModCollectionNodeState state) => state switch
        {
            ModCollectionNodeState.Installed => "mod_manager/collection_state_installed",
            ModCollectionNodeState.Disabled => "mod_manager/collection_state_disabled",
            ModCollectionNodeState.InCatalog => "mod_manager/collection_state_catalog",
            ModCollectionNodeState.VersionMismatch => "mod_manager/collection_state_mismatch",
            _ => "mod_manager/collection_state_missing",
        };

        /// <summary>Titlebar/badge color for a state.</summary>
        public static Color Color(ModCollectionNodeState state) => state switch
        {
            ModCollectionNodeState.Installed => UiTokens.Success,
            ModCollectionNodeState.Disabled => UiTokens.Warning,
            ModCollectionNodeState.InCatalog => UiTokens.Info,
            ModCollectionNodeState.VersionMismatch => UiTokens.Warning,
            _ => UiTokens.Danger,
        };
    }
}
