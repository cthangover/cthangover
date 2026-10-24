namespace Cthangover.Mods.Stats
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The registered character sheet sections, filled per mod assembly by <see cref="SheetSectionConsumer"/>.</summary>
    public static class SheetSections
    {
        private static readonly List<ISheetSection> Sections = new();

        /// <summary>The sections in display order.</summary>
        public static IReadOnlyList<ISheetSection> All => Sections.OrderBy(s => s.Order).ThenBy(s => s.Id).ToList();

        /// <summary>Adds a section.</summary>
        public static void Add(ISheetSection section)
        {
            if (section != null && !Sections.Contains(section))
                Sections.Add(section);
        }

        /// <summary>Removes a section.</summary>
        public static void Remove(ISheetSection section) => Sections.Remove(section);
    }
}
