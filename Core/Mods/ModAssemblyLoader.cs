using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    public static class ModAssemblyLoader
    {
        static ModAssemblyLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnModAssemblyResolve;
        }

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

        public static void RegisterAssembly(Assembly assembly, string modId)
        {
            Actions.ScenarioActionFactory.Instance.RegisterAssembly(assembly);
            Scenarios.ScenarioCommandStrategyFactory.RegisterAssembly(assembly);
            Scenes.SceneEventRegistry.RegisterAssembly(assembly);
            Items.ItemActionFactory.RegisterAssembly(assembly);
            Battle.BattleCoreRegistry.Instance.RegisterAssembly(assembly);
            Relationship.RecruitBehaviourRegistry.Instance.RegisterAssembly(assembly);
            UI.Tool.ToolFactory.Instance.RegisterAssembly(assembly);
            UI.Tool.ToolBoxButtonFactory.Instance.RegisterAssembly(assembly);
            ModInitializerRegistry.RegisterAssembly(assembly, modId);
        }

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
