using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Merges everything known about mods into the manager's grid model: local
    /// mods discovered by <see cref="ModRegistry"/> (with enable state and
    /// deletability) and online catalog entries from <see cref="CatalogService"/>.
    /// A mod present in both sources becomes one combined card; catalog-only mods
    /// appear as installable tiles.
    /// <para/>
    /// The model also owns the session overrides that keep the UI honest after
    /// file operations: a mod installed or deleted from the manager is not yet
    /// reflected by the registry (which is loaded once per launch), so
    /// <see cref="MarkInstalled"/> / <see cref="MarkRemoved"/> adjust the view and
    /// the restart banner tells the player when the change becomes real.
    /// </summary>
    public class ModManagerModel
    {
        /// <summary>Id of the mod that renders this UI; it can never be disabled or removed.</summary>
        public const string SelfModId = "mod_manager";

        private readonly List<ModCardData> _items = new();
        private readonly HashSet<string> _removed = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _installed = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>All cards, unsorted and unfiltered; rebuild with <see cref="Reload"/>.</summary>
        public IReadOnlyList<ModCardData> Items => _items;

        /// <summary>
        /// Rebuilds the card list from the current registry, disabled store and
        /// catalog cache. Safe to call on every action completion and after a
        /// catalog refresh.
        /// </summary>
        public void Reload()
        {
            _items.Clear();

            var disabled = DisabledModsStore.Load();
            var variantsById = BuildCatalogVariants();
            var localIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var ordered = ModRegistry.Instance.GetOrderedModIds(filterDisabled: false);
            foreach (var id in ordered)
            {
                if (_removed.Contains(id))
                    continue;

                if (!ModRegistry.Instance.AllDiscoveredMods.TryGetValue(id, out var info) || info is not ModInfo modInfo)
                    continue;

                localIds.Add(id);
                var isDisabled = disabled.Contains(id);

                if (variantsById.TryGetValue(id, out var variants))
                {
                    foreach (var variant in variants)
                        _items.Add(BuildLocalCard(id, modInfo, isDisabled, variant));
                }
                else
                {
                    _items.Add(BuildLocalCard(id, modInfo, isDisabled, null));
                }
            }

            foreach (var pair in variantsById)
            {
                if (localIds.Contains(pair.Key) || _removed.Contains(pair.Key))
                    continue;

                foreach (var variant in pair.Value)
                    _items.Add(BuildRemoteCard(variant));
            }
        }

        /// <summary>Remembers that a mod was deleted this session so it stays hidden until restart.</summary>
        public void MarkRemoved(string modId)
        {
            _removed.Add(modId);
            _installed.Remove(modId);
        }

        /// <summary>Remembers a successful install so the card shows as installed before restart.</summary>
        public void MarkInstalled(string modId, string version)
        {
            _removed.Remove(modId);
            _installed[modId] = version ?? string.Empty;
        }

        /// <summary>
        /// Applies an enable/disable toggle with the same cascade as the legacy
        /// menu: disabling a mod also disables its dependents, enabling one also
        /// re-enables the dependencies it needs.
        /// </summary>
        public void SetEnabled(string modId, bool enabled)
        {
            var disabled = DisabledModsStore.Load();
            if (enabled)
            {
                disabled.Remove(modId);
                var info = ModRegistry.Instance.GetMod(modId) as ModInfo;
                var dependencies = info?.Manifest?.DependencyIds;
                if (dependencies != null)
                {
                    foreach (var dependency in dependencies)
                        disabled.Remove(dependency);
                }
            }
            else
            {
                disabled.Add(modId);
                CollectDependents(modId, disabled);
            }

            DisabledModsStore.Save(disabled);
        }

        /// <summary>Category buckets for the sidebar, respecting every filter except the category set.</summary>
        public Dictionary<CategoryType, int> CountCategories(ModFilterState filter)
        {
            var counts = new Dictionary<CategoryType, int>();
            foreach (var item in _items)
            {
                if (!filter.MatchesExceptCategory(item))
                    continue;

                foreach (var category in ModCategoryNames.Ordered)
                {
                    if ((item.Category & category) == 0)
                        continue;
                    counts[category] = counts.GetValueOrDefault(category) + 1;
                }
            }

            return counts;
        }

        /// <summary>Status buckets for the sidebar, respecting every filter except the status set.</summary>
        public Dictionary<ModItemStatus, int> CountStatuses(ModFilterState filter)
        {
            var counts = new Dictionary<ModItemStatus, int>();
            foreach (var item in _items)
            {
                if (!filter.MatchesExceptStatus(item))
                    continue;
                counts[item.Status] = counts.GetValueOrDefault(item.Status) + 1;
            }

            return counts;
        }

        /// <summary>
        /// Indexes catalog entries by mod id. Normally one entry per id, but a
        /// variant conflict (same id/version/title, different content across
        /// catalogs) produces several entries that the UI shows as separate mods.
        /// </summary>
        private Dictionary<string, List<CatalogExportEntry>> BuildCatalogVariants()
        {
            var index = new Dictionary<string, List<CatalogExportEntry>>(StringComparer.OrdinalIgnoreCase);
            var entries = CatalogService.CachedEntries;
            if (entries == null)
                return index;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry?.Id))
                    continue;

                if (!index.TryGetValue(entry.Id, out var list))
                {
                    list = new List<CatalogExportEntry>();
                    index[entry.Id] = list;
                }

                list.Add(entry);
            }

            return index;
        }

        private ModCardData BuildLocalCard(string id, ModInfo modInfo, bool disabled, CatalogExportEntry catalog)
        {
            var data = new ModCardData
            {
                Id = id,
                Title = modInfo.DisplayTitle,
                Author = modInfo.Author,
                Description = modInfo.LocalizedDescription,
                LocalVersion = modInfo.Version,
                Category = modInfo.Manifest?.Category ?? CategoryType.Other,
                LocalInfo = modInfo,
                Catalog = catalog,
                IsInstalled = true,
                IsDisabled = disabled,
                IsDeletable = ModUninstaller.CanUninstall(modInfo),
                IsProtected = string.Equals(id, SelfModId, StringComparison.OrdinalIgnoreCase),
            };

            if (_installed.TryGetValue(id, out var installedVersion))
                data.LocalVersion = installedVersion;

            ApplyDownloads(data, catalog);
            data.RecomputeStatus();
            return data;
        }

        private ModCardData BuildRemoteCard(CatalogExportEntry catalog)
        {
            var locale = GetCatalogLocale();
            var data = new ModCardData
            {
                Id = catalog.Id,
                Title = catalog.GetLocalizedName(locale),
                Author = catalog.Author,
                Description = catalog.GetLocalizedDescription(locale),
                Category = catalog.Category,
                Catalog = catalog,
                IsInstalled = false,
            };

            if (_installed.TryGetValue(catalog.Id, out var installedVersion))
            {
                data.IsInstalled = true;
                data.LocalVersion = installedVersion;
            }

            ApplyDownloads(data, catalog);
            data.RecomputeStatus();
            return data;
        }

        private static void ApplyDownloads(ModCardData data, CatalogExportEntry catalog)
        {
            data.Downloads = catalog?.Downloads ?? new List<CatalogEntryDownload>();
            data.SelectedDownload = data.Downloads.Count > 0 ? data.Downloads[0] : null;
        }

        private static string GetCatalogLocale()
        {
            var locale = TranslationServer.GetLocale();
            return string.IsNullOrEmpty(locale) ? "en" : locale;
        }

        private static void CollectDependents(string modId, HashSet<string> disabled)
        {
            var allMods = ModRegistry.Instance.AllDiscoveredMods;
            foreach (var pair in allMods)
            {
                if (disabled.Contains(pair.Key) || pair.Value is not ModInfo info)
                    continue;

                var dependencies = info.Manifest?.DependencyIds;
                if (dependencies != null && dependencies.Contains(modId))
                {
                    disabled.Add(pair.Key);
                    CollectDependents(pair.Key, disabled);
                }
            }
        }
    }
}
