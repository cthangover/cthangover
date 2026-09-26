using Cthangover.Core.UI.Theming;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Locale keys, badge colors and sort ranks for <see cref="ModItemStatus"/>.
    /// Colors are semantic <see cref="UiTokens"/> entries shared with the rest of
    /// the UI: success installed, warning update, info downgrade, danger locally
    /// modified, muted disabled, faint not installed.
    /// </summary>
    public static class ModStatusNames
    {
        /// <summary>Localized status label shown on tiles and the detail page.</summary>
        public static string Translate(ModItemStatus status)
            => TranslationServer.Translate(Key(status));

        /// <summary>Translation key of a status.</summary>
        public static string Key(ModItemStatus status) => status switch
        {
            ModItemStatus.Installed => "mod_manager/status_installed",
            ModItemStatus.UpdateAvailable => "mod_manager/status_update",
            ModItemStatus.DowngradeAvailable => "mod_manager/status_downgrade",
            ModItemStatus.ModifiedLocally => "mod_manager/status_modified",
            ModItemStatus.Disabled => "mod_manager/status_disabled",
            _ => "mod_manager/status_not_installed",
        };

        /// <summary>Badge color for a status.</summary>
        public static Color Color(ModItemStatus status) => status switch
        {
            ModItemStatus.Installed => UiTokens.Success,
            ModItemStatus.UpdateAvailable => UiTokens.Warning,
            ModItemStatus.DowngradeAvailable => UiTokens.Info,
            ModItemStatus.ModifiedLocally => UiTokens.Danger,
            ModItemStatus.Disabled => UiTokens.Muted,
            _ => UiTokens.Faint,
        };

        /// <summary>
        /// Rank used by <see cref="ModSortMode.Status"/>: highlights actionable
        /// states (update, modified) before plain installed and catalog-only mods.
        /// </summary>
        public static int SortRank(ModItemStatus status) => status switch
        {
            ModItemStatus.UpdateAvailable => 0,
            ModItemStatus.ModifiedLocally => 1,
            ModItemStatus.Installed => 2,
            ModItemStatus.DowngradeAvailable => 3,
            ModItemStatus.Disabled => 4,
            _ => 5,
        };
    }
}
