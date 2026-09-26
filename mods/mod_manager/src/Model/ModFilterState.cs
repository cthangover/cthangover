using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Mods.ModManager
{
    /// <summary>
    /// Live filter state of the manager. Empty status/category sets mean "all",
    /// which matches Nexus-style sidebars where checkboxes only ever narrow the
    /// list. Search matches title, id and author case-insensitively. The state
    /// also owns the sort mode and the optional grouping toggle.
    /// </summary>
    public class ModFilterState
    {
        /// <summary>Free-text query matched against title, id and author.</summary>
        public string Search { get; set; } = string.Empty;

        /// <summary>Selected statuses; empty means no status restriction.</summary>
        public HashSet<ModItemStatus> Statuses { get; } = new();

        /// <summary>Selected categories; empty means no category restriction.</summary>
        public HashSet<CategoryType> Categories { get; } = new();

        /// <summary>Grid ordering mode.</summary>
        public ModSortMode Sort { get; set; } = ModSortMode.Name;

        /// <summary>When true the grid renders category headers between groups.</summary>
        public bool GroupByCategory { get; set; }

        /// <summary>Full match: search + status + category.</summary>
        public bool Matches(ModCardData item)
            => MatchesSearch(item) && MatchesStatuses(item) && MatchesCategories(item);

        /// <summary>Match ignoring the category set — used to compute category counts.</summary>
        public bool MatchesExceptCategory(ModCardData item)
            => MatchesSearch(item) && MatchesStatuses(item);

        /// <summary>Match ignoring the status set — used to compute status counts.</summary>
        public bool MatchesExceptStatus(ModCardData item)
            => MatchesSearch(item) && MatchesCategories(item);

        /// <summary>Returns matching items in the configured order.</summary>
        public List<ModCardData> Apply(IEnumerable<ModCardData> source)
        {
            var result = new List<ModCardData>();
            foreach (var item in source)
            {
                if (Matches(item))
                    result.Add(item);
            }

            result.Sort(Compare);
            return result;
        }

        /// <summary>Clears search, statuses and categories; keeps sort and grouping.</summary>
        public void Reset()
        {
            Search = string.Empty;
            Statuses.Clear();
            Categories.Clear();
        }

        private int Compare(ModCardData a, ModCardData b)
        {
            switch (Sort)
            {
                case ModSortMode.Author:
                    var byAuthor = string.Compare(a.Author, b.Author, StringComparison.OrdinalIgnoreCase);
                    return byAuthor != 0 ? byAuthor : CompareTitle(a, b);

                case ModSortMode.Status:
                    var byStatus = ModStatusNames.SortRank(a.Status).CompareTo(ModStatusNames.SortRank(b.Status));
                    return byStatus != 0 ? byStatus : CompareTitle(a, b);

                default:
                    return CompareTitle(a, b);
            }
        }

        private static int CompareTitle(ModCardData a, ModCardData b)
            => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);

        private bool MatchesSearch(ModCardData item)
        {
            if (string.IsNullOrWhiteSpace(Search))
                return true;

            var query = Search.Trim();
            return Contains(item.Title, query) || Contains(item.Id, query) || Contains(item.Author, query);
        }

        private bool MatchesStatuses(ModCardData item)
            => Statuses.Count == 0 || Statuses.Contains(item.Status);

        private bool MatchesCategories(ModCardData item)
            => Categories.Count == 0 || (item.Category & AsFlags()) != 0;

        private CategoryType AsFlags()
        {
            var flags = (CategoryType)0;
            foreach (var category in Categories)
                flags |= category;
            return flags;
        }

        private static bool Contains(string value, string query)
            => !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
