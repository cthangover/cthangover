namespace Cthangover.Core.Mods.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.Compilation;
    using Cthangover.Core.Mods.Unload;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Replaces the running mod set inside the process — the mechanism behind
    /// switching games without a restart and behind hot reload of edited mods.
    /// Two halves with different thread and failure contracts:
    /// <list type="number">
    /// <item><see cref="Prepare"/> (pure .NET, background-safe): discovery and
    /// filtering for a launch selection into fresh dictionaries, compilation
    /// of every mod into the cache (dependents against the artifacts their
    /// dependencies produced right now), staging of precompiled DLLs. Nothing
    /// running is touched, so a compile error simply discards the result.</item>
    /// <item><see cref="Apply"/> (main thread): computes the
    /// <see cref="ModReloadPlan"/> from code identities, releases the mods that
    /// leave or changed through the full <see cref="ModUnloader"/> protocol,
    /// re-registers the kept mods (their context, statics and live nodes
    /// survive), swaps the registry dictionaries, activates the new game
    /// choice, loads the new contexts and registers everything in dependency
    /// order — the same order a cold boot uses.</item>
    /// </list>
    /// Derived state that is not per-assembly (registries rebuilt from data,
    /// locale, scenes, runtime) is rebuilt by the caller,
    /// <see cref="ModManager"/>, around <see cref="Apply"/>.
    /// </summary>
    public sealed class ModSetReloader
    {
        private readonly ModRegistry registry;
        private readonly ModBootPipeline pipeline;
        private readonly Dictionary<string, IModInfo> mods;
        private readonly Dictionary<string, IModInfo> allDiscoveredMods;
        private readonly Dictionary<string, int> existingPriorities;

        internal ModSetReloader(ModRegistry registry, ModBootPipeline pipeline, Dictionary<string, IModInfo> mods, Dictionary<string, IModInfo> allDiscoveredMods, Dictionary<string, int> existingPriorities)
        {
            this.registry = registry;
            this.pipeline = pipeline;
            this.mods = mods;
            this.allDiscoveredMods = allDiscoveredMods;
            this.existingPriorities = existingPriorities;
        }

        /// <summary>
        /// Discovers, filters and compiles the mod set of <paramref name="selection"/>
        /// without loading it. Safe on a background thread. Check
        /// <see cref="ModReloadPreparation.Succeeded"/> before applying.
        /// </summary>
        public ModReloadPreparation Prepare(GameLaunchArgs selection, IReadOnlyCollection<string> force, CancellationToken cancellationToken = default)
        {
            var prep = new ModReloadPreparation(selection, force);
            lock (pipeline.PipelineLock)
            {
                prep.PreviousArtifacts = new Dictionary<string, string>(ModAssemblyCache.CurrentArtifacts, StringComparer.OrdinalIgnoreCase);
                foreach (var (scanner, url, priority) in pipeline.ResolvedRoots)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (scanner.IsCanProvide(url))
                        ModDiscoveryService.DiscoverMods(prep.Mods, prep.Priorities, scanner, url, priority, cancellationToken);
                }

                ModTestSuites.Mount(prep.Mods, new List<string>(selection.UserArgs));
                foreach (var kvp in prep.Mods)
                    prep.AllDiscovered[kvp.Key] = kvp.Value;

                ModSetFilter.FilterDisabledMods(prep.Mods);
                prep.Choice = ModSetFilter.SelectActiveGame(prep.Mods, selection);
                ModSetFilter.ValidateDependencies(prep.Mods);

                prep.Precompiled = ModAssemblyLoader.StagePrecompiledDlls(prep.Mods.Values, cancellationToken);
                prep.Compiled = ModCompiler.PrepareAll(prep.Mods, cancellationToken);
                prep.Errors.AddRange(GameLogger.CompilationErrors);
            }

            if (prep.Succeeded)
                GameLogger.Log("MODS_RELOAD", $"Prepared mod set: {prep.Mods.Count} mod(s), {prep.Compiled.Count} compiled, {prep.Precompiled.Count} precompiled DLL(s)");
            else
                GameLogger.Log("MODS_RELOAD", $"Mod set preparation FAILED with {prep.Errors.Count} error(s); the running set stays", LogLevel.Error);
            return prep;
        }

        /// <summary>
        /// Throws away a preparation that will not be applied: its file
        /// providers are disposed and the artifact table goes back to the
        /// files the running contexts were compiled against.
        /// </summary>
        public void Discard(ModReloadPreparation prep)
        {
            if (prep == null)
                return;
            ModAssemblyCache.CurrentArtifacts.Clear();
            foreach (var kvp in prep.PreviousArtifacts)
                ModAssemblyCache.CurrentArtifacts[kvp.Key] = kvp.Value;
            foreach (var mod in prep.AllDiscovered.Values)
            {
                if (!mods.TryGetValue(mod.Id, out var running) || !ReferenceEquals(running.FileProvider, mod.FileProvider))
                    mod.FileProvider?.Dispose();
            }
        }

        /// <summary>
        /// Switches the process to the prepared set. Main thread only, outside
        /// any signal emission of a node that may be freed. Returns the plan and
        /// the unload ticket for verification.
        /// </summary>
        public ModReloadResult Apply(ModReloadPreparation prep, SceneTree tree)
        {
            lock (pipeline.PipelineLock)
            {
                var targetOrder = TargetOrder(prep);
                var loadedOrder = LoadedOrder();
                var plan = ModReloadPlan.Compute(loadedOrder, LoadedArtifacts(loadedOrder), targetOrder, TargetArtifacts(prep), TargetDependencies(prep), prep.Force);
                GameLogger.Log("MODS_RELOAD", $"Plan: unload [{string.Join(", ", plan.Unload)}], keep [{string.Join(", ", plan.Keep)}], load [{string.Join(", ", plan.Load)}]");

                var ticket = ModUnloader.Unload(plan.Unload, tree);
                var keptInUnloadOrder = new List<string>(plan.Keep);
                keptInUnloadOrder.Reverse();
                ModUnloader.UnregisterKept(keptInUnloadOrder);
                SwapMods(prep);

                var load = new HashSet<string>(plan.Load, StringComparer.OrdinalIgnoreCase);
                var loaded = LoadNewContexts(prep, load);
                RegisterInOrder(prep, targetOrder, load, loaded);
                LoadNewGdScripts(prep);

                registry.InvalidateOrder();
                var orderedIds = registry.GetOrderedModIds();
                var order = new List<(string ModId, int Priority)>();
                for (var i = 0; i < orderedIds.Count; i++)
                    order.Add((orderedIds[i], i));
                ScriptTypeRegistry.SetModOrder(order);
                registry.InitializedFlag = true;

                var game = ActiveGame.Id ?? (ActiveGame.IsFrontendOnly ? "<front-end>" : "<all mods>");
                GameLogger.Log("MODS_RELOAD", $"Mod set applied: {mods.Count} mod(s), game='{game}'");
                return new ModReloadResult(true, plan, ticket, null);
            }
        }

        private void SwapMods(ModReloadPreparation prep)
        {
            foreach (var old in allDiscoveredMods.Values)
            {
                if (!prep.AllDiscovered.TryGetValue(old.Id, out var replacement) || !ReferenceEquals(replacement.FileProvider, old.FileProvider))
                    old.FileProvider?.Dispose();
            }

            mods.Clear();
            foreach (var kvp in prep.Mods)
                mods[kvp.Key] = kvp.Value;
            allDiscoveredMods.Clear();
            foreach (var kvp in prep.AllDiscovered)
                allDiscoveredMods[kvp.Key] = kvp.Value;
            existingPriorities.Clear();
            foreach (var kvp in prep.Priorities)
                existingPriorities[kvp.Key] = kvp.Value;

            GameLaunchArgs.SetCurrent(prep.Selection);
            prep.Choice.Apply();
            registry.InvalidateOrder();
        }

        /// <summary>Loads precompiled DLLs and compiled artifacts of the mods in <paramref name="load"/> into new contexts, without registering them.</summary>
        private static Dictionary<string, List<Assembly>> LoadNewContexts(ModReloadPreparation prep, HashSet<string> load)
        {
            var nativeMods = new List<IModInfo>();
            foreach (var mod in prep.Mods.Values)
            {
                if (load.Contains(mod.Id))
                    nativeMods.Add(mod);
            }
            ModNativeLoader.LoadNativeDlls(nativeMods);

            var staged = new List<(string ModId, string CachedPath)>();
            foreach (var entry in prep.Precompiled)
            {
                if (load.Contains(entry.ModId))
                    staged.Add(entry);
            }

            var loaded = new Dictionary<string, List<Assembly>>(StringComparer.OrdinalIgnoreCase);
            using (ModAssemblyLoader.BeginBatch())
            {
                foreach (var (modId, assembly) in ModAssemblyLoader.LoadStagedDlls(staged))
                    Add(loaded, modId, assembly);
                foreach (var compiled in prep.Compiled)
                {
                    if (!load.Contains(compiled.ModId))
                        continue;
                    var assembly = ModAssemblyLoader.LoadFromPath(compiled.ArtifactPath, compiled.ModId);
                    if (assembly != null)
                        Add(loaded, compiled.ModId, assembly);
                    else
                        GameLogger.Log("MODS_RELOAD", $"Mod '{compiled.ModId}' compiled but did not load from '{compiled.ArtifactPath}'", LogLevel.Error);
                }
            }
            return loaded;
        }

        /// <summary>Fans every target assembly out in dependency order: fresh ones from <paramref name="loaded"/>, kept ones from their live contexts.</summary>
        private static void RegisterInOrder(ModReloadPreparation prep, List<string> targetOrder, HashSet<string> load, Dictionary<string, List<Assembly>> loaded)
        {
            var sources = new Dictionary<string, List<(string FilePath, string Source)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var compiled in prep.Compiled)
                sources[compiled.ModId] = compiled.Sources;

            foreach (var modId in targetOrder)
            {
                var assemblies = new List<Assembly>();
                if (load.Contains(modId))
                {
                    if (loaded.TryGetValue(modId, out var fresh))
                        assemblies.AddRange(fresh);
                }
                else
                {
                    foreach (var record in ModAssemblyLoader.Records(new[] { modId }))
                        assemblies.AddRange(record.Assemblies);
                }

                foreach (var assembly in assemblies)
                {
                    ModAssemblyLoader.RegisterAssembly(assembly, modId);
                    if (sources.TryGetValue(modId, out var modSources) && assembly.GetName().Name == modId)
                        ScriptTypeRegistry.RegisterAssembly(assembly, modId, modSources);
                }
            }
        }

        /// <summary>GDScripts load once per mod and process (their registrations cannot be withdrawn); mods that left keep theirs until restart.</summary>
        private static void LoadNewGdScripts(ModReloadPreparation prep)
        {
            var fresh = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in prep.Mods)
            {
                if (!Contains(ModGDScriptLoader.LoadedModIds, kvp.Key))
                    fresh[kvp.Key] = kvp.Value;
            }
            ModGDScriptLoader.LoadGDScripts(fresh);

            foreach (var id in ModGDScriptLoader.LoadedModIds)
            {
                if (!prep.Mods.ContainsKey(id))
                    GameLogger.Log("MODS_RELOAD", $"GDScript registrations of mod '{id}' stay until the next restart (GDScript mods are not unloadable)", LogLevel.Warning);
            }
        }

        /// <summary>Mods with live contexts in their current load order (dependencies first).</summary>
        private List<string> LoadedOrder()
        {
            var loaded = new HashSet<string>(ModAssemblyLoader.LoadedModIds, StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (var id in registry.GetOrderedModIds())
            {
                if (loaded.Remove(id))
                    order.Add(id);
            }
            order.AddRange(loaded);
            return order;
        }

        private static Dictionary<string, IReadOnlyCollection<string>> LoadedArtifacts(List<string> loadedOrder)
        {
            var result = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in loadedOrder)
                result[id] = ModAssemblyLoader.ArtifactsOf(id);
            return result;
        }

        /// <summary>Target mods that have code (compiled or precompiled), in dependency order.</summary>
        private static List<string> TargetOrder(ModReloadPreparation prep)
        {
            var withCode = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var compiled in prep.Compiled)
                withCode.Add(compiled.ModId);
            foreach (var (modId, _) in prep.Precompiled)
                withCode.Add(modId);

            var infos = new List<ModInfo>();
            foreach (var mod in prep.Mods.Values)
            {
                if (mod is ModInfo { Manifest: not null } info)
                    infos.Add(info);
            }

            var order = new List<string>();
            foreach (var info in ModCompileOrder.TopologicalSort(infos))
            {
                if (withCode.Contains(info.Id))
                    order.Add(info.Id);
            }
            return order;
        }

        private static Dictionary<string, IReadOnlyCollection<string>> TargetArtifacts(ModReloadPreparation prep)
        {
            var paths = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (modId, path) in prep.Precompiled)
                AddPath(paths, modId, path);
            foreach (var compiled in prep.Compiled)
                AddPath(paths, compiled.ModId, compiled.ArtifactPath);

            var result = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in paths)
                result[kvp.Key] = kvp.Value;
            return result;
        }

        private static Dictionary<string, IReadOnlyCollection<string>> TargetDependencies(ModReloadPreparation prep)
        {
            var result = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in prep.Mods.Values)
            {
                if (mod is ModInfo info)
                    result[info.Id] = info.Manifest?.DependencyIds ?? new List<string>();
            }
            return result;
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var candidate in ids)
            {
                if (string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void AddPath(Dictionary<string, List<string>> map, string modId, string path)
        {
            if (!map.TryGetValue(modId, out var list))
                map[modId] = list = new List<string>();
            list.Add(path);
        }

        private static void Add(Dictionary<string, List<Assembly>> map, string modId, Assembly assembly)
        {
            if (!map.TryGetValue(modId, out var list))
                map[modId] = list = new List<Assembly>();
            list.Add(assembly);
        }
    }
}
