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
            => FilterDisabledMods(mods, Games.GameLaunchArgs.Current.WithMods);

        /// <summary>
        /// <see cref="FilterDisabledMods(IDictionary{string, IModInfo})"/> where the mods named
        /// in <paramref name="explicitIds"/> (<c>--with-mods</c>) stay enabled for this run,
        /// see <see cref="DisabledFor"/>.
        /// </summary>
        public static void FilterDisabledMods(IDictionary<string, IModInfo> mods, IReadOnlyCollection<string> explicitIds)
        {
            var disabled = DisabledFor(mods, explicitIds, log: true);
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

                    // A disabled optional dependency is simply absent: the mod still loads without it.
                    var depIds = ((ModInfo)kvp.Value).Manifest?.RequiredDependencyIds;
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
        /// The user's disabled mods (<see cref="DisabledModsStore"/>) minus the mods named
        /// explicitly for this run (<c>--with-mods</c>) and their required dependencies,
        /// transitively: naming a mod on the command line is a stronger wish than the
        /// mod manager's switch, and only for this launch — the stored list is not changed.
        /// </summary>
        public static HashSet<string> DisabledFor(
            IDictionary<string, IModInfo> mods, IReadOnlyCollection<string> explicitIds, bool log = false)
        {
            var disabled = DisabledModsStore.Load();
            if (disabled.Count == 0 || explicitIds == null || explicitIds.Count == 0)
                return disabled;

            var lookup = new Dictionary<string, IModInfo>(mods, System.StringComparer.OrdinalIgnoreCase);
            var wanted = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>(explicitIds);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!wanted.Add(id) || !lookup.TryGetValue(id, out var info))
                    continue;
                foreach (var depId in ((ModInfo)info).Manifest?.RequiredDependencyIds ?? new List<string>())
                    pending.Push(depId);
            }

            var result = new HashSet<string>();
            foreach (var id in disabled)
            {
                if (!wanted.Contains(id))
                {
                    result.Add(id);
                    continue;
                }

                if (log)
                    GameLogger.Log("MODS_REGISTRY", $"Mod '{id}' is disabled but named in --with-mods; enabled for this run");
            }

            return result;
        }

        /// <summary>
        /// Validates every remaining mod's <c>depends</c> entries and removes
        /// mods whose dependencies cannot be satisfied: a malformed dependency
        /// spec, a missing required mod (optional dependencies are skipped),
        /// or a loaded version failing the declared constraint. First failing
        /// dependency per mod wins (loop breaks); the reason carries both the
        /// requirement and the actually loaded version. Removal cascades: the
        /// pass repeats until nothing more is removed, so a mod whose required
        /// dependency was itself dropped is dropped too instead of compiling
        /// against a mod that is gone.
        /// </summary>
        public static void ValidateDependencies(IDictionary<string, IModInfo> mods)
        {
            while (ValidateDependenciesOnce(mods))
            {
            }
        }

        /// <summary>One validation pass; true when it removed a mod.</summary>
        private static bool ValidateDependenciesOnce(IDictionary<string, IModInfo> mods)
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

            foreach (var id in toRemove)
            {
                mods.Remove(id);
                GameLogger.Log("MODS_REGISTRY", $"Mod '{id}' filtered out (dependency validation failed)", LogLevel.Message);
            }
            return toRemove.Count > 0;
        }

        /// <summary>
        /// Keeps only the mods of the game chosen with <c>--game=&lt;id&gt;</c>
        /// and makes it the <see cref="Games.ActiveGame"/> (boot path); see
        /// <see cref="SelectActiveGame"/>.
        /// </summary>
        public static void FilterToActiveGame(IDictionary<string, IModInfo> mods)
            => SelectActiveGame(mods, Games.GameLaunchArgs.Current).Apply();

        /// <summary>
        /// Keeps only the mods of the game in <paramref name="args"/>: its
        /// collection's resolved graph, every front-end collection and the
        /// <c>--with-mods</c> extras (see <see cref="Games.GameModSet"/>). The set is
        /// recomputed from the current collection every time, so the player's
        /// edits apply on the next boot or game switch. Without <c>--game</c> (or with
        /// <c>--all-mods</c>) nothing is filtered; an unknown game id logs an
        /// error and also loads every mod, so a stale shortcut still starts.
        /// Returns the choice without applying it (an in-process reload applies
        /// it only once the new set compiled).
        /// </summary>
        public static Games.ActiveGameChoice SelectActiveGame(IDictionary<string, IModInfo> mods, Games.GameLaunchArgs args)
        {
            if (args.Mode == Games.GameLaunchMode.AllMods)
            {
                RemoveFixtures(mods, args.WithMods);
                return Games.ActiveGameChoice.None;
            }

            // Mounted test suites ride along like --with-mods (with their depends).
            var extras = new List<string>(args.WithMods);
            extras.AddRange(ModTestSuites.ActiveIds);

            var discovered = new Dictionary<string, IModInfo>(mods, System.StringComparer.OrdinalIgnoreCase);
            var collections = new Dictionary<string, ModCollection>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var collection in ModCollectionStore.LoadAll())
                collections[collection.Id] = collection;

            if (args.Mode == Games.GameLaunchMode.Frontend)
            {
                var frontend = Games.GameModSet.ComputeFrontend(collections, discovered, DisabledFor(mods, args.WithMods), extras);
                if (frontend == null)
                {
                    GameLogger.Log("MODS_REGISTRY", "No front-end collection installed; loading every mod", LogLevel.Error);
                    return Games.ActiveGameChoice.None;
                }

                RemoveOutside(mods, frontend.ModIds, "front-end");
                return new Games.ActiveGameChoice(frontend, frontend: true);
            }

            var set = Games.GameModSet.Compute(args.GameId, collections, discovered, DisabledFor(mods, args.WithMods), extras);
            if (set == null)
            {
                GameLogger.Log("MODS_REGISTRY", $"Game '{args.GameId}' is not a known game collection; loading every mod", LogLevel.Error);
                return Games.ActiveGameChoice.None;
            }

            foreach (var missing in set.MissingMods)
                GameLogger.Log("MODS_REGISTRY", $"Game '{args.GameId}' needs mod '{missing}', which is not installed", LogLevel.Error);
            foreach (var problem in set.Problems)
                GameLogger.Log("MODS_REGISTRY", $"Game '{args.GameId}': {problem.Title}", LogLevel.Warning);

            RemoveOutside(mods, set.ModIds, args.GameId);
            return new Games.ActiveGameChoice(set, frontend: false);
        }

        /// <summary>
        /// <c>--all-mods</c> leaves out test fixtures (<see cref="ModManifest.Fixture"/>)
        /// unless they are named in <c>--with-mods</c>: their data would leak into
        /// every unrelated dev launch and test.
        /// </summary>
        public static void RemoveFixtures(IDictionary<string, IModInfo> mods, IReadOnlyCollection<string> withMods)
        {
            var wanted = new HashSet<string>(withMods ?? new List<string>(), System.StringComparer.OrdinalIgnoreCase);
            foreach (var id in new List<string>(mods.Keys))
            {
                if (mods[id] is not ModInfo { Manifest.Fixture: true } info || wanted.Contains(id))
                    continue;
                info.LoadFailureReasons.Add($"test fixture: loaded only with --with-mods={id}");
                mods.Remove(id);
                GameLogger.Log("MODS_REGISTRY", $"Fixture '{id}' left out (name it in --with-mods to load it)");
            }
        }

        private static void RemoveOutside(IDictionary<string, IModInfo> mods, ICollection<string> keep, string owner)
        {
            var excluded = new List<string>();
            foreach (var id in new List<string>(mods.Keys))
            {
                if (keep.Contains(id))
                    continue;

                ((ModInfo)mods[id]).LoadFailureReasons.Add(string.Format(Tr("mods/not_in_game_reason"), owner));
                mods.Remove(id);
                excluded.Add(id);
            }

            GameLogger.Log("MODS_REGISTRY",
                $"Mod set '{owner}': {mods.Count} mod(s) loaded, {excluded.Count} left out ({string.Join(", ", excluded)})");
        }

        /// <summary>
        /// Translates a localization key only on the Godot main thread. The
        /// discovery/validation phase may run on a background thread during boot
        /// (before any locale is loaded anyway), so <c>TranslationServer.Translate</c>
        /// must not be called there — the raw key is returned instead.
        /// </summary>
        private static string Tr(string key)
        {
            if (!GameEnvironment.IsRuntime || !GameEnvironment.IsMainThread)
                return key;
            return TranslationServer.Translate(key);
        }
    }
}
