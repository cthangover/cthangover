using Cthangover.Core.Mods;

namespace Mods.ModManager
{
    /// <summary>
    /// Presentation names for <see cref="CategoryType"/>. Kept inside the
    /// mod_manager mod (keys prefixed <c>mod_manager/category_</c>) so the manager
    /// does not depend on the interface mod's locale for its filter sidebar.
    /// </summary>
    public static class ModCategoryNames
    {
        /// <summary>Display order of category filters and group headers.</summary>
        public static readonly CategoryType[] Ordered =
        {
            CategoryType.Story,
            CategoryType.Characters,
            CategoryType.Gameplay,
            CategoryType.Visual,
            CategoryType.Audio,
            CategoryType.UI,
            CategoryType.Locale,
            CategoryType.Library,
            CategoryType.Engine,
            CategoryType.Overhaul,
            CategoryType.Tools,
            CategoryType.Editor,
            CategoryType.Debug,
            CategoryType.Other,
        };

        /// <summary>
        /// First single category of a (possibly combined) flags value, used to
        /// place a mod into exactly one grid group and one count bucket per
        /// category it belongs to.
        /// </summary>
        public static CategoryType Primary(CategoryType category)
        {
            foreach (var candidate in Ordered)
            {
                if ((category & candidate) != 0)
                    return candidate;
            }
            return CategoryType.Other;
        }

        /// <summary>Localized category name for filters and group headers.</summary>
        public static string Translate(CategoryType category)
            => Godot.TranslationServer.Translate(Key(category));

        /// <summary>Translation key of a single (non-combined) category.</summary>
        public static string Key(CategoryType category) => category switch
        {
            CategoryType.Story => "mod_manager/category_story",
            CategoryType.Characters => "mod_manager/category_characters",
            CategoryType.Gameplay => "mod_manager/category_gameplay",
            CategoryType.Visual => "mod_manager/category_visual",
            CategoryType.Audio => "mod_manager/category_audio",
            CategoryType.UI => "mod_manager/category_ui",
            CategoryType.Locale => "mod_manager/category_locale",
            CategoryType.Library => "mod_manager/category_library",
            CategoryType.Engine => "mod_manager/category_engine",
            CategoryType.Overhaul => "mod_manager/category_overhaul",
            CategoryType.Tools => "mod_manager/category_tools",
            CategoryType.Editor => "mod_manager/category_editor",
            CategoryType.Debug => "mod_manager/category_debug",
            _ => "mod_manager/category_other",
        };
    }
}
