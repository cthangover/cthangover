namespace Cthangover.Core.Mods.Pipeline
{
    using System.Collections.Generic;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Pure set-filters over the discovered mod dictionary, run once by
    /// <c>ModBootPipeline.DiscoverAndCompile</c> after all roots are scanned:
    /// <see cref="FilterDisabledMods"/> removes user-disabled mods and
    /// transitively their dependents; <see cref="ValidateDependencies"/>
    /// removes mods whose declared dependencies are malformed, missing, or
    /// version-mismatched. Both record a human-readable reason on every
    /// removed mod's <c>ModInfo.LoadFailureReasons</c> (appended, never
    /// overwritten — the mod manager UI shows the full list) and keep the
    /// whole mod visible in <c>AllDiscoveredMods</c>, so filtering only
    /// affects the loadable set, never the discovery record.
    ///
    /// Stateless by design: the dictionary is passed in, reasons are stamped
    /// on the <see cref="ModInfo"/> objects themselves, and translation goes
    /// through <see cref="Tr"/> so the phase stays safe on a background
    /// thread during launcher boot.
    /// </summary>
    public static class ModSetFilter
    {
        /// <summary>
        /// Removes disabled mods from <paramref name="mods"/> and then, in a
        /// fixpoint loop, every mod that (transitively) depends on a removed
        /// one — an optional dependency on a disabled mod does NOT disable
        /// the dependent. The loop is needed because dependents may appear
        /// before their dependencies in dictionary order; it repeats until no
        /// new mod is marked, so chains of arbitrary depth collapse.
        /// </summary>
        public static void FilterDisabledMods(IDictionary<string, IModInfo> mods)
        {
            var disabled = DisabledModsStore.Load();
            if (disabled.Count == 0)
                return;

            var toRemove = new HashSet<string>();
            foreach (var id in mods.Keys)
            {
                if (disabled.Contains(id))
                {
                    toRemove.Add(id);
                    var info = (ModInfo)mods[id];
                    info.LoadFailureReasons.Add(string.Format(Tr("mods/disabled_reason"), id));
                }
            }

            bool changed;
            do
            {
                changed = false;
                foreach (var kvp in mods)
                {
                    if (toRemove.Contains(kvp.Key))
                        continue;

                    var manifest = ((ModInfo)kvp.Value).Manifest;
                    var depIds = manifest?.DependencyIds;
                    if (depIds == null || depIds.Count == 0)
                        continue;

                    foreach (var depId in depIds)
                    {
                        if (toRemove.Contains(depId))
                        {
                            toRemove.Add(kvp.Key);
                            var info = (ModInfo)kvp.Value;
                            info.LoadFailureReasons.Add(string.Format(Tr("mods/dep_disabled_reason"), depId));
                            GameLogger.Log("MODS_REGISTRY",
                                $"Mod '{kvp.Key}' disabled because dependency '{depId}' is disabled",
                                LogLevel.Warning);
                            changed = true;
                            break;
                        }
                    }
                }
            } while (changed);

            foreach (var id in toRemove)
            {
                mods.Remove(id);
                GameLogger.Log("MODS_REGISTRY", $"Mod '{id}' filtered out (disabled)", LogLevel.Message);
            }
        }

        /// <summary>
        /// Validates every remaining mod's <c>depends</c> entries and removes
        /// mods whose dependencies cannot be satisfied: a malformed dependency
        /// spec, a missing required mod (optional dependencies are skipped),
        /// or a loaded version failing the declared constraint. First failing
        /// dependency per mod wins (loop breaks); the reason carries both the
        /// requirement and the actually loaded version.
        /// </summary>
        public static void ValidateDependencies(IDictionary<string, IModInfo> mods)
        {
            var toRemove = new HashSet<string>();

            foreach (var kvp in mods)
            {
                var modId = kvp.Key;
                var manifest = ((ModInfo)kvp.Value).Manifest;
                if (manifest?.Depends == null)
                    continue;

                foreach (var dep in manifest.Depends)
                {
                    if (!dep.IsValid)
                    {
                        toRemove.Add(modId);
                        var info = (ModInfo)kvp.Value;
                        info.LoadFailureReasons.Add(string.Format(Tr("mods/invalid_dep_format"), dep.Error));
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{modId}' has invalid dependency: {dep.Error}",
                            LogLevel.Error);
                        break;
                    }

                    if (!mods.ContainsKey(dep.ModId))
                    {
                        if (dep.IsOptional)
                            continue;

                        toRemove.Add(modId);
                        var info = (ModInfo)kvp.Value;
                        info.LoadFailureReasons.Add(string.Format(Tr("mods/required_mod_not_loaded"), dep.ModId));
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{modId}' requires '{dep.ModId}' but it is not loaded",
                            LogLevel.Error);
                        break;
                    }

                    if (!dep.IsSatisfiedBy(mods[dep.ModId].Version ?? "0.0.0.0"))
                    {
                        toRemove.Add(modId);
                        var info = (ModInfo)kvp.Value;
                        var actualVersion = mods[dep.ModId].Version ?? "0.0.0.0";
                        info.LoadFailureReasons.Add(
                            string.Format(Tr("mods/version_mismatch"),
                                $"{dep.ModId}{dep.Operator}{dep.VersionRaw}", actualVersion));
                        GameLogger.Log("MODS_REGISTRY",
                            $"Mod '{modId}' requires {dep.ModId}{dep.Operator}{dep.VersionRaw} but loaded version is {actualVersion}",
                            LogLevel.Error);
                        break;
                    }
                }
            }

            if (toRemove.Count == 0)
                return;

            foreach (var id in toRemove)
            {
                mods.Remove(id);
                GameLogger.Log("MODS_REGISTRY", $"Mod '{id}' filtered out (dependency validation failed)", LogLevel.Message);
            }
        }

        /// <summary>
        /// Translates a localization key only on the Godot main thread. The
        /// discovery/validation phase may run on a background thread during boot
        /// (before any locale is loaded anyway), so <c>TranslationServer.Translate</c>
        /// must not be called there — the raw key is returned instead.
        /// </summary>
        private static string Tr(string key)
        {
            if (!GameEnvironment.IsMainThread)
                return key;
            return TranslationServer.Translate(key);
        }
    }
}
