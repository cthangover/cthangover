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
        /// the enable list was written. Never throws for download failures —
        /// they are logged and reported through the return value.
        /// </summary>
        public static async Task<bool> ApplyAsync(ModCollectionPlan plan, ISet<string> sessionInstalled)
        {
            if (plan == null || plan.IsEmpty || plan.Missing.Count > 0)
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

            return true;
        }
    }
}
