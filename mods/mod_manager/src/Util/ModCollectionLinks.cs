using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Shared texts for the mod-to-collection cross links required by the hub
    /// mockup: mod tiles and the detail page show a clickable "in N
    /// collections" badge that jumps to the collections tab. Centralized so
    /// the card and the detail page always render the same pluralized string.
    /// </summary>
    public static class ModCollectionLinks
    {
        /// <summary>Localized "in N collection(s)" label for the given count; empty when the mod belongs to no game.</summary>
        public static string BadgeText(int count)
        {
            if (count <= 0)
                return string.Empty;

            var key = count == 1
                ? "mod_manager/hub_in_one_collection"
                : "mod_manager/hub_in_many_collections";
            return string.Format(TranslationServer.Translate(key), count);
        }
    }
}
