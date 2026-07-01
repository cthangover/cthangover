using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Loads compiled assemblies (DLLs) from mods and registers their
    /// types into every reflection-driven subsystem in the game.
    ///
    /// <b>Precompiled DLL discovery</b> — <c>LoadPrecompiledDlls</c>
    /// recursively scans each mod's <c>dll/</c> directory for
    /// <c>.dll</c> files, copies them to the assembly cache to give
    /// them a stable filesystem path, and registers each one.
    ///
    /// <b>Registration fan-out</b> — <c>RegisterAssembly</c> is the
    /// single point through which every new assembly enters the game.
    /// It calls into every subsystem that uses reflection to find
    /// implementations: scenario actions, command strategies, scene
    /// events, item actions, battle behaviour, recruit behaviours,
    /// tool factories, toolbox button factories, and mod initialisers.
    /// Adding a new reflection-based plugin system to the game requires
    /// adding exactly one line here.
    ///
    /// <b>Assembly resolve hook</b> — the static constructor subscribes
    /// to <c>AppDomain.AssemblyResolve</c> so that when a mod DLL
    /// references another mod DLL, the runtime can find it by name
    /// among already-loaded assemblies. Without this, inter-mod type
    /// references would throw <c>TypeLoadException</c> at runtime.
    /// </summary>
    public static class ModAssemblyLoader
    {
        static ModAssemblyLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnModAssemblyResolve;
        }

        /// <summary>
        /// Loads a <c>.dll</c> assembly from raw bytes. Returns null
        /// on failure — the caller is responsible for logging.
        /// </summary>
        public static Assembly LoadFromBytes(byte[] raw)
        {
            try
            {
                return Assembly.Load(raw);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadFromBytes: loading raw data fail: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Registers all types from a newly-loaded assembly into every
        /// reflection-based plugin system. This is the single point
        /// that fans a new assembly out to all registries.
        /// </summary>
        public static void RegisterAssembly(Assembly assembly, string modId)
        {
            ScenarioActionFactory.Instance.RegisterAssembly(assembly);
            ScenarioCommandStrategyFactory.RegisterAssembly(assembly);
            Scenes.SceneEventRegistry.RegisterAssembly(assembly);
            ItemActionFactory.RegisterAssembly(assembly);
            Battle.BattleCoreRegistry.Instance.RegisterAssembly(assembly);
            Relationship.RecruitBehaviourRegistry.Instance.RegisterAssembly(assembly);
            UI.Tool.ToolFactory.Instance.RegisterAssembly(assembly);
            UI.Tool.ToolBoxButtonFactory.Instance.RegisterAssembly(assembly);
            ModInitializerRegistry.RegisterAssembly(assembly, modId);
        }

        /// <summary>
        /// Scans each loaded mod's <c>dll/</c> directory for precompiled
        /// <c>.dll</c> files, copies them to the assembly cache, and
        /// registers them. Runs before source compilation so that
        /// source mods can reference precompiled API types.
        /// </summary>
        public static void LoadPrecompiledDlls(IEnumerable<IModInfo> mods)
        {
            if (!Directory.Exists(ModCompiler.CacheRoot))
                Directory.CreateDirectory(ModCompiler.CacheRoot);

            foreach (var mod in mods)
            {
                var dllFiles = CollectDllFiles(mod.FileProvider, mod.Id);
                if (dllFiles.Count == 0)
                    continue;

                GameLogger.Log("MODS_ASSEMBLY", $"ModAssemblyLoader.LoadPrecompiledDlls: found {dllFiles.Count} precompiled DLL(s) in mod '{mod.Id}'");

                foreach (var (filePath, bytes) in dllFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var cachedPath = Path.Combine(ModCompiler.CacheRoot, $"{mod.Id}_dll_{fileName}");

                        File.WriteAllBytes(cachedPath, bytes);

                        var assembly = LoadFromBytes(bytes);
                        if (assembly != null)
                        {
                            RegisterAssembly(assembly, mod.Id);
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
                if (!asm.IsDynamic && asm.GetName().Name == name)
                    return asm;
            }
            return null;
        }
    }
}
