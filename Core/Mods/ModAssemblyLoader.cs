using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Mods.Consumers;
using Cthangover.Core.Mods.Discovery;
using Cthangover.Core.Scenes.TscnBuild;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Loads compiled assemblies (DLLs) from mods and registers their types
    /// into every reflection-driven subsystem in the game.
    ///
    /// <b>Precompiled DLL discovery</b> — <c>LoadPrecompiledDlls</c>
    /// recursively scans each mod's <c>dll/</c> directory for <c>.dll</c>
    /// files, copies them to the assembly cache to give them a stable
    /// filesystem path, and loads each one.
    ///
    /// <b>Collectible contexts</b> — each mod's assemblies load into its own
    /// collectible <see cref="ModLoadContext"/> (see the type's remarks for
    /// the resolution rules). <see cref="LoadFromPath"/>/<see cref="LoadFromBytes"/>
    /// route through the context staged for the current compile batch; they
    /// keep working for callers outside the mod pipeline (test engine,
    /// in-game script editor) which land in the default context as before.
    ///
    /// <b>Registration fan-out</b> — <see cref="RegisterAssembly"/> is the
    /// single point through which every new assembly enters the game. The
    /// subsystem list is declarative: every consumer implements
    /// <see cref="IAssemblyConsumer"/> and is discovered in the kernel
    /// assembly; mods may contribute consumers of their own. Adding a new
    /// reflection-based plugin system to the game means adding one consumer
    /// class — no edits here.
    ///
    /// <b>Unload</b> — <see cref="UnloadMod"/> runs the reverse fan-out
    /// (<see cref="AssemblyConsumerRegistry.FanOutUnloading"/>), unregisters
    /// the script paths and asks the context to release. It is idempotent per
    /// mod and safe to call for unknown mod ids (no-op with a log line).
    /// </summary>
    public static class ModAssemblyLoader
    {
        static ModAssemblyLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnModAssemblyResolve;
        }

        /// <summary>Tracked, fanned-out assemblies per mod id, keyed by mod id.</summary>
        private static readonly Dictionary<string, LoadedModAssembly> loadedMods = new(StringComparer.OrdinalIgnoreCase);

        private static readonly object loaderLock = new();

        private static ModLoadContext ResolveDependencyContext(string assemblySimpleName)
        {
            lock (loaderLock)
            {
                // Dependency mods compile first (topological order) and their
                // context is registered the moment their entry assembly loads,
                // so by the time a dependent resolves, the dependency context
                // already exists here.
                return loadedMods.Values
                    .Where(l => l.Context != null && l.Context.HasAssembly(assemblySimpleName))
                    .Select(l => l.Context)
                    .FirstOrDefault();
            }
        }

        private static ModLoadContext AcquireContext(string modId)
        {
            lock (loaderLock)
            {
                if (loadedMods.TryGetValue(modId, out var existing))
                    return existing.Context;

                // Collectible contexts are created only while the mod pipeline
                // (boot or reload) drives the load. Ad-hoc compilation — the
                // test engine, the in-game script editor, unit tests calling
                // ModCompiler.Compile directly — stays in the default context:
                // an unload nobody will ever call would only add a mid-unload
                // assembly to later AppDomain scans.
                if (!pipelineBatchActive)
                    return null;

                // First assembly of the mod (compiled dll or first precompiled
                // one): create its collectible context and register it, so the
                // dependents resolve inter-mod types through it.
                var context = new ModLoadContext(modId, ResolveDependencyContext);
                loadedMods[modId] = new LoadedModAssembly { ModId = modId, Context = context };
                return context;
            }
        }

        /// <summary>
        /// Begins a pipeline-driven load batch: while active, mod assembly
        /// loads route into collectible contexts. Called by the boot pipeline
        /// around its load phases and by ModRegistry.Reload; ad-hoc loads
        /// outside the batch stay in the default context.
        /// </summary>
        public static IDisposable BeginBatch()
        {
            lock (loaderLock)
                pipelineBatchActive = true;
            return new BatchScope();
        }

        /// <summary>Ends the load batch (see BeginBatch).</summary>
        public static void EndBatch()
        {
            lock (loaderLock)
                pipelineBatchActive = false;
        }

        private static bool pipelineBatchActive;

        private sealed class BatchScope : IDisposable
        {
            public void Dispose() => EndBatch();
        }

        /// <summary>
        /// Loads a cached mod assembly from its file path into the mod's
        /// collectible context. File-backed loading keeps the runtime-resolved
        /// portable PDB working, giving debuggers line-level breakpoints in
        /// mod code and stacks with file:line (plan-mod-testing.md §9.2).
        /// Returns null on failure — callers fall back to
        /// <see cref="LoadFromBytes"/>.
        /// </summary>
        public static Assembly LoadFromPath(string dllPath)
        {
            return LoadFromPath(dllPath, null);
        }

        /// <summary>Context-aware overload: routes the load into <paramref name="modId"/>'s context when the pipeline batch is active; outside a batch the load lands in the default context.</summary>
        public static Assembly LoadFromPath(string dllPath, string modId)
        {
            try
            {
                ModLoadContext context = null;
                string stagedName = null;
                if (modId != null)
                {
                    context = AcquireContext(modId);
                    if (context != null)
                    {
                        stagedName = context.StageAssembly(dllPath);
                        lock (loaderLock)
                        {
                            if (!loadedMods.TryGetValue(modId, out var record) || record.Context == null)
                            {
                                if (record == null)
                                    loadedMods[modId] = record = new LoadedModAssembly { ModId = modId };
                                record.Context = context;
                            }
                        }
                    }
                }

                // Staged by the assembly's own simple name, not the file name:
                // cached compiled mods live in "<id>_<hash>.dll".
                var assembly = context != null
                    ? context.LoadStaged(stagedName)
                    : Assembly.LoadFrom(dllPath);
                if (assembly == null)
                    throw new InvalidOperationException($"assembly '{stagedName}' was staged but did not resolve in context '{context?.Name}'");

                RegisterScriptPaths(assembly);
                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadFromPath: loaded '{assembly.GetName().Name}' (context={(context?.Name ?? "default")}, pdb={System.IO.File.Exists(System.IO.Path.ChangeExtension(dllPath, ".pdb"))})");
                return assembly;
            }
            catch (Exception ex)
            {
                // Error, not warning: the byte fallback loses the PDB (no
                // breakpoints, no file:line in stacks) and the collectible
                // context (no unload), so a failure here must be visible.
                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadFromPath: '{dllPath}' failed, falling back to byte load without symbols: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Loads a mod's separately compiled test assembly
        /// (<c>{modId}__tests</c>) file-backed into its own collectible
        /// context. Tests reference the mod's production assembly, which lives
        /// in a collectible context; a default-context (non-collectible)
        /// assembly may not reference a collectible one, so the test assembly
        /// needs a context of its own that resolves mod assemblies through
        /// their contexts (type identity stays shared with the running mod).
        /// The context is not tracked for unload: test mode quits the process.
        /// Returns null on failure (logged as an error).
        /// </summary>
        public static Assembly LoadTestAssembly(string dllPath, string modId)
        {
            try
            {
                var context = new ModLoadContext(modId + "__tests", ResolveDependencyContext);
                var name = context.StageAssembly(dllPath);
                var assembly = context.LoadStaged(name)
                    ?? throw new InvalidOperationException($"test assembly '{name}' did not resolve in its context");
                RegisterScriptPaths(assembly);
                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadTestAssembly: loaded '{name}' (context={context.Name}, pdb={File.Exists(Path.ChangeExtension(dllPath, ".pdb"))})");
                return assembly;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadTestAssembly: '{dllPath}' failed: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Loads a <c>.dll</c> assembly from raw bytes. Returns null
        /// on failure — the caller is responsible for logging.
        /// </summary>
        public static Assembly LoadFromBytes(byte[] raw)
        {
            try
            {
                var assembly = Assembly.Load(raw);
                RegisterScriptPaths(assembly);
                return assembly;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadFromBytes: loading raw data fail: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Feeds the assembly to GodotSharp's script path database so classes
        /// carrying <see cref="Godot.ScriptPathAttribute"/> get their lifecycle
        /// callbacks (<c>_EnterTree</c>, <c>_Ready</c>, <c>_Process</c>, ...)
        /// dispatched by the engine. Godot only scans the project assembly on
        /// its own; runtime-loaded mod assemblies must be registered here.
        /// The call is a no-op for assemblies without the
        /// <see cref="Godot.AssemblyHasScriptsAttribute"/> marker.
        /// </summary>
        private static void RegisterScriptPaths(Assembly assembly)
        {
            if (assembly == null)
                return;

            try
            {
                Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.RegisterScriptPaths: '{assembly.GetName().Name}' failed: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Registers all types from a newly-loaded assembly into every
        /// reflection-based plugin system through the declarative consumer
        /// registry, and records the registration for the unload pass.
        /// </summary>
        public static void RegisterAssembly(Assembly assembly, string modId)
        {
            if (assembly == null)
                return;

            AssemblyConsumerRegistry.EnsureInitialized();
            AssemblyConsumerRegistry.FanOutLoaded(assembly, modId);

            // Mod-contributed consumers join the registry too — after the
            // fan-out of the assembly that declared them, so they observe the
            // remaining mods but not themselves (mirroring how initializers
            // get OnModLoaded catch-up).
            AssemblyConsumerRegistry.RegisterFromAssembly(assembly);

            lock (loaderLock)
            {
                if (!loadedMods.TryGetValue(modId, out var record))
                    loadedMods[modId] = record = new LoadedModAssembly { ModId = modId };
                if (!record.Assemblies.Contains(assembly))
                    record.Assemblies.Add(assembly);
            }
        }

        /// <summary>
        /// Unloads one mod's assemblies: reverse fan-out through the consumer
        /// registry, then release of the collectible context. The context
        /// cannot be released while anything still holds a reference — the
        /// diagnostic is logged, never thrown, and the failure mode degrades
        /// to the pre-collectible behaviour (assemblies stay in memory until
        /// process exit) instead of crashing the game.
        /// </summary>
        /// <returns>Null on success, otherwise the leak diagnostic string.</returns>
        public static string UnloadMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return null;

            LoadedModAssembly record;
            lock (loaderLock)
            {
                if (!loadedMods.TryGetValue(modId, out record))
                    return null;
                loadedMods.Remove(modId);
            }

            foreach (var assembly in record.Assemblies)
                AssemblyConsumerRegistry.FanOutUnloading(assembly, modId);

            GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.UnloadMod: reverse fan-out done for '{modId}' ({record.Assemblies.Count} assembly/ies)");

            if (record.Context == null)
                return "loaded in default context (no collectible context recorded)";

            return record.Context.TryUnload();
        }

        /// <summary>True when the mod has assemblies tracked for a possible unload.</summary>
        public static bool IsModLoaded(string modId)
        {
            lock (loaderLock)
                return loadedMods.ContainsKey(modId);
        }

        /// <summary>Snapshot of mod ids that currently have tracked assemblies (unload candidates).</summary>
        public static IReadOnlyList<string> LoadedModIds
        {
            get
            {
                lock (loaderLock)
                    return loadedMods.Keys.ToList();
            }
        }

        /// <summary>Test/setup helper: forgets every tracked mod without running the unload protocol.</summary>
        public static void ClearTracking()
        {
            lock (loaderLock)
                loadedMods.Clear();
        }

        /// <summary>
        /// Scans each loaded mod's <c>dll/</c> directory for precompiled
        /// <c>.dll</c> files, copies them to the assembly cache, and loads
        /// each as an <see cref="Assembly"/> WITHOUT registering it.
        /// Registration fan-out (see <see cref="RegisterAssembly"/>) must be
        /// performed separately, on the main thread, after this returns — this
        /// keeps the file I/O and <c>Assembly.Load</c> work thread-safe for the
        /// background boot phase while deferring anything that touches Godot.
        /// </summary>
        /// <returns>A list of <c>(modId, assembly)</c> pairs for every loaded
        /// precompiled DLL, in discovery order.</returns>
        public static List<(string ModId, Assembly Assembly)> LoadPrecompiledDlls(IEnumerable<IModInfo> mods, CancellationToken cancellationToken = default)
        {
            var result = new List<(string ModId, Assembly Assembly)>();

            if (!Directory.Exists(ModCompiler.CacheRoot))
                Directory.CreateDirectory(ModCompiler.CacheRoot);

            foreach (var mod in mods)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dllFiles = CollectDllFiles(mod.FileProvider, mod.Id);
                if (dllFiles.Count == 0)
                    continue;

                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadPrecompiledDlls: found {dllFiles.Count} precompiled DLL(s) in mod '{mod.Id}'");

                foreach (var (filePath, bytes) in dllFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var cachedPath = Path.Combine(ModCompiler.CacheRoot, $"{mod.Id}_dll_{fileName}");

                        File.WriteAllBytes(cachedPath, bytes);

                        var assembly = LoadFromPath(cachedPath, mod.Id) ?? LoadFromBytes(bytes);
                        if (assembly != null)
                        {
                            ModNativeLoader.InstallResolverOnAssembly(assembly);
                            result.Add((mod.Id, assembly));
                            GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadPrecompiledDlls: loaded precompiled DLL '{filePath}' from mod '{mod.Id}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        GameLogger.CompilationErrors.Add($"Mod '{mod.Id}', DLL '{filePath}': {ex.Message}");
                        GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadPrecompiledDlls: failed to load precompiled DLL '{filePath}' from mod '{mod.Id}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            return result;
        }

        private static List<(string FilePath, byte[] Bytes)> CollectDllFiles(IModFileProvider provider, string modId)
        {
            var result = new List<(string, byte[])>();
            CollectDllFilesRecursive(provider, "dll", result);
            return result;
        }

        private static void CollectDllFilesRecursive(IModFileProvider provider, string directory, List<(string, byte[])> result)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectDllFilesRecursive(provider, subDir, result);
                }
                else if (entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = provider.ReadFileBinary(entry);
                    if (bytes != null)
                        result.Add((entry, bytes));
                }
            }
        }

        private static Assembly OnModAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Collectible (mod ALC) assemblies must never be handed out
                // through the default-context hook: resolving a non-collectible
                // assembly into a collectible one throws
                // "Operation is not supported" (0x80131515). Inter-mod
                // resolution is owned by ModLoadContext.Load instead.
                if (asm.IsCollectible || asm.IsDynamic)
                    continue;

                if (asm.GetName().Name == name)
                    return asm;
            }
            return null;
        }
    }
}
