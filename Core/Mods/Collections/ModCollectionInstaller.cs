using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Shared "apply a collection plan" routine for every surface that can
    /// install a collection: the mod hub gallery, the scheme editor and the
    /// main menu's game picker. Downloads every <see cref="ModCollectionPlan.Install"/> item
    /// through <see cref="ModDownloader"/> in graph order and, when all of them
    /// succeeded, re-enables the <see cref="ModCollectionPlan.Enable"/> ids in
    /// <c>disabled_mods.json</c> — the same file operations the collections
    /// screen has always performed on apply. Ids of mods installed during this
    /// session are added to the caller's set so both the registry-blind UI and
    /// a restart banner stay honest until the next launch.
    /// </summary>
    public static class ModCollectionInstaller
    {
        /// <summary>
        /// Applies the plan; returns true when every download succeeded and
        /// the enable list was written. When a collection is supplied, also saves
        /// its catalog documents so the selection survives an offline restart.
        /// Never throws for download failures —
        /// they are logged and reported through the return value.
        /// </summary>
        public static async Task<bool> ApplyAsync(ModCollectionPlan plan, ISet<string> sessionInstalled,
            ModCollection collection = null, IReadOnlyDictionary<string, ModCollection> collections = null)
        {
            if (plan == null || plan.HasProblems || plan.IsEmpty && collection?.Origin != ModCollectionOrigin.Catalog)
                return false;

            foreach (var item in plan.Install)
            {
                bool success;
                try
                {
                    success = await ModDownloader.InstallAsync(item.ModId, item.Version, item.SourceUrl);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MOD_COLLECTIONS", $"Install '{item.ModId}' failed: {ex.Message}", LogLevel.Error);
                    success = false;
                }

                if (!success)
                    return false;

                sessionInstalled?.Add(item.ModId);
            }

            var disabled = DisabledModsStore.Load();
            var changed = false;
            foreach (var id in plan.Enable)
                changed |= disabled.Remove(id);
            if (changed)
                DisabledModsStore.Save(disabled);

            try
            {
                if (collection != null && !await InstallPackages(collection, collections, new HashSet<string>(StringComparer.OrdinalIgnoreCase))) return false;
            }
            catch (Exception error)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Collection package failed: {error.Message}", LogLevel.Error);
                return false;
            }
            return ModCollectionStore.SaveCatalogSelection(collection, collections);
        }

        private static async Task<bool> InstallPackages(ModCollection collection, IReadOnlyDictionary<string, ModCollection> available, HashSet<string> visited)
        {
            if (!visited.Add(collection.Id)) return true;
            foreach (var include in collection.Includes ?? new())
                if (include.IsSelected && available != null && available.TryGetValue(ModDependency.Parse(include.Ref).ModId, out var child)
                    && !await InstallPackages(child, available, visited)) return false;
            return await CollectionPackageDownload.InstallAsync(collection);
        }
    }
}
