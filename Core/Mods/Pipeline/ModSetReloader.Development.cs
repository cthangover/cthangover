namespace Cthangover.Core.Mods.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.Compilation;

    public sealed partial class ModSetReloader
    {
        internal ModReloadPreparation PrepareDevelopment(GameLaunchArgs selection, string id, bool attach,
            CancellationToken cancellationToken)
        {
            var prep = new ModReloadPreparation(selection, null)
            {
                CompileScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id }
            };
            lock (pipeline.PipelineLock)
            {
                prep.PreviousArtifacts = new(ModAssemblyCache.CurrentArtifacts, StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var pair in mods)
                        prep.Mods.Add(pair.Key, pair.Value);
                    foreach (var pair in allDiscoveredMods)
                        prep.AllDiscovered.Add(pair.Key, pair.Value);
                    foreach (var pair in existingPriorities)
                        prep.Priorities.Add(pair.Key, pair.Value);

                    if (attach && !prep.Mods.ContainsKey(id))
                        DiscoverDevelopmentAdditions(prep, id, cancellationToken);
                    if (!prep.Mods.TryGetValue(id, out var target))
                        prep.Errors.Add($"Mod '{id}' is unavailable. Use mod.attach for a newly installed mod.");
                    else if (target is not ModInfo { Manifest: not null, FileProvider.RootPath: not null })
                        prep.Errors.Add($"Mod '{id}' needs a writable folder installation.");
                    if (!prep.Succeeded)
                        return prep;

                    var dependencies = TargetDependencies(prep);
                    foreach (var pair in dependencies)
                    {
                        if (DependsOnScope(pair.Key, dependencies, prep.CompileScope, new(StringComparer.OrdinalIgnoreCase)))
                            prep.CompileScope.Add(pair.Key);
                    }

                    var staged = prep.Mods.Values.Where(mod => prep.CompileScope.Contains(mod.Id)).ToArray();
                    foreach (var mod in staged)
                    {
                        if (mod is ModInfo { Manifest.GdSources.Count: > 0 } && mods.ContainsKey(mod.Id))
                            prep.Errors.Add($"'{mod.Id}' has active GDScript registrations and requires a process restart.");
                    }
                    if (!prep.Succeeded)
                        return prep;

                    ModAssemblyCache.CurrentArtifacts.Clear();
                    prep.Precompiled = ModAssemblyLoader.StagePrecompiledDlls(staged, cancellationToken);
                    foreach (var mod in prep.Mods.Values)
                    {
                        if (prep.CompileScope.Contains(mod.Id))
                            continue;
                        foreach (var path in ModAssemblyLoader.ArtifactsOf(mod.Id))
                        {
                            if (ModAssemblyCache.IsSelfCacheDll(Path.GetFileName(path), mod.Id))
                                ModAssemblyCache.CurrentArtifacts[mod.Id] = path;
                            else
                                prep.Precompiled.Add((mod.Id, path));
                        }
                    }

                    prep.Compiled = ModCompiler.PrepareAll(prep.Mods, cancellationToken, prep.CompileScope);
                    prep.Errors.AddRange(Cthangover.Core.Utils.GameLogger.CompilationErrors);
                    return prep;
                }
                catch
                {
                    Discard(prep);
                    throw;
                }
            }
        }

        internal ModReloadPlan Preview(ModReloadPreparation preparation)
        {
            lock (pipeline.PipelineLock)
                return Plan(preparation, TargetOrder(preparation));
        }

        private ModReloadPlan Plan(ModReloadPreparation prep, List<string> targetOrder)
        {
            var order = LoadedOrder();
            return ModReloadPlan.Compute(order, LoadedArtifacts(order), targetOrder,
                TargetArtifacts(prep), TargetDependencies(prep), prep.Force);
        }

        private void DiscoverDevelopmentAdditions(ModReloadPreparation prep, string id, CancellationToken token)
        {
            var candidates = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase);
            var priorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var (scanner, url, priority) in pipeline.ResolvedRoots)
                {
                    token.ThrowIfCancellationRequested();
                    if (scanner.IsCanProvide(url))
                        ModDiscoveryService.DiscoverMods(candidates, priorities, scanner, url, priority, token);
                }

                var pending = new Stack<string>();
                pending.Push(id);
                while (pending.TryPop(out var next))
                {
                    if (prep.Mods.ContainsKey(next))
                        continue;
                    if (!candidates.TryGetValue(next, out var candidate))
                    {
                        prep.Errors.Add($"Installed dependency '{next}' was not found.");
                        continue;
                    }
                    prep.Mods.Add(next, candidate);
                    prep.AllDiscovered[next] = candidate;
                    prep.Priorities[next] = priorities.GetValueOrDefault(next);
                    prep.CompileScope.Add(next);
                    if (candidate is ModInfo info)
                    {
                        foreach (var dependency in info.Manifest.RequiredDependencyIds ?? Enumerable.Empty<string>())
                            pending.Push(dependency);
                    }
                }

                var validation = new Dictionary<string, IModInfo>(prep.Mods, StringComparer.OrdinalIgnoreCase);
                ModSetFilter.ValidateDependencies(validation);
                foreach (var mod in prep.Mods.Keys)
                {
                    if (!validation.ContainsKey(mod))
                        prep.Errors.Add($"Cannot satisfy dependencies for '{mod}'.");
                }
            }
            finally
            {
                var borrowed = new HashSet<IModFileProvider>(ReferenceEqualityComparer.Instance);
                foreach (var candidate in prep.Mods.Values)
                    borrowed.Add(candidate.FileProvider);
                foreach (var candidate in candidates.Values)
                {
                    if (candidate.FileProvider != null && borrowed.Add(candidate.FileProvider))
                        candidate.FileProvider.Dispose();
                }
            }
        }

        private static bool DependsOnScope(string id, IReadOnlyDictionary<string, IReadOnlyCollection<string>> dependencies,
            IReadOnlySet<string> scope, HashSet<string> visited)
        {
            if (!visited.Add(id) || !dependencies.TryGetValue(id, out var direct))
                return false;
            foreach (var dependency in direct)
            {
                if (scope.Contains(dependency) || DependsOnScope(dependency, dependencies, scope, visited))
                    return true;
            }
            return false;
        }
    }
}
