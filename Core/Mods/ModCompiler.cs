using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cthangover.Core.Utils;
using Godot;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Roslyn-based C# compiler for mod source files. The compilation
    /// pipeline is:
    ///
    /// 1. <b>Source collection</b> — walks each mod's declared source
    ///    patterns (from <c>manifest.json</c>'s <c>sources</c> list),
    ///    matching <c>.cs</c> files. Falls back to scanning the entire
    ///    mod root if no patterns are declared, so a mod that just
    ///    drops <c>.cs</c> files anywhere still compiles.
    ///
    /// 2. <b>Topological sort</b> — orders mods by their
    ///    <c>depends</c> declarations so that a mod referencing another
    ///    mod's types compiles after its dependency. Circular
    ///    dependencies log a warning and break the cycle rather than
    ///    deadlocking.
    ///
    /// 3. <b>Assembly reference discovery</b> — scans every loaded
    ///    assembly's directory and the Godot build output directory
    ///    for <c>.dll</c> files, adding them as metadata references
    ///    so that mod code can call into any type in the game.
    ///
    /// 4. <b>SHA-256 output caching</b> — the compilation output is
    ///    stored at <c>user://mod_cache/assemblies/{modId}_{hash}.dll</c>.
    ///    On subsequent loads the hash is recomputed from source files;
    ///    if it matches a cached DLL, recompilation is skipped
    ///    entirely. The <c>use_assembly_cache</c> config toggle can
    ///    force recompilation even when a cached DLL exists.
    ///
    /// 5. <b>Registration</b> — successful assemblies are passed to
    ///    <c>ModAssemblyLoader.RegisterAssembly</c>, which fans them
    ///    out to every reflection-based plugin registry in the game.
    ///
    /// <c>CompileString</c> is a separate entry point for ad-hoc code
    /// compilation (used by tools and the in-game script editor).
    /// </summary>
    public static class ModCompiler
    {
        
        /// <summary>
        /// Absolute path to the assembly cache directory
        /// (<c>user://mod_cache/assemblies/</c>). Resolved once at
        /// static init time with a fallback to the OS temp folder.
        /// </summary>
        public static readonly string CacheRoot;

        static ModCompiler()
        {
            try
            {
                CacheRoot = ProjectSettings.GlobalizePath("user://mod_cache/assemblies/");
            }
            catch
            {
                CacheRoot = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "cthangover", "mod_cache", "assemblies");
            }

        }

        /// <summary>
        /// Compiles all mods that declare source files in their
        /// manifest. Mods are sorted topologically by dependencies
        /// before compilation. Results are cached on disk by SHA-256
        /// content hash.
        /// </summary>
        public static void LoadModCode(IDictionary<string, IModInfo> mods)
        {
            GameLogger.CompilationErrors.Clear();

            if(!Directory.Exists(CacheRoot))
                Directory.CreateDirectory(CacheRoot);
            
            var modsWithCode = new List<ModInfo>();
            foreach (var kvp in mods)
            {
                var modInfo = kvp.Value as ModInfo;
                if (modInfo?.Manifest?.Sources == null || modInfo.Manifest.Sources.Count == 0)
                    continue;
                modsWithCode.Add(modInfo);
            }

            if (modsWithCode.Count == 0)
                return;

            var compiled = new HashSet<string>();
            var sorted = TopologicalSort(modsWithCode);

            foreach (var modInfo in sorted)
                {
                    try
                    {
                        var sourceFiles = CollectSourceFiles(modInfo);
                        if (sourceFiles.Count == 0)
                            continue;

                        GameLogger.Log("MODS_COMPILE", $"ModCompiler.LoadModCode: compiling mod '{modInfo.Id}' with {sourceFiles.Count} source file(s):");
                        foreach (var (path, _) in sourceFiles)
                            GameLogger.Log("MODS_COMPILE", $"  {path}", LogLevel.Debug);

                        var assembly = Compile(modInfo.Id, sourceFiles);
                        if (assembly != null)
                        {
                            compiled.Add(modInfo.Id);
                            ModAssemblyLoader.RegisterAssembly(assembly, modInfo.Id);

                            GameLogger.Log("MODS_COMPILE", $"ModCompiler.LoadModCode: Compiled and loaded code for mod '{modInfo.Id}', {sourceFiles.Count} source file(s)");
                        }
                }
                catch (Exception ex)
                {
                    GameLogger.CompilationErrors.Add($"Mod '{modInfo.Id}': {ex.Message}");
                    GameLogger.Log("MODS_COMPILE", $"ModCompiler.LoadModCode: Failed to compile mod '{modInfo.Id}': {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                }
            }
        }

        private static List<(string FilePath, string Source)> CollectSourceFiles(ModInfo modInfo)
        {
            var result = new List<(string, string)>();

            foreach (var pattern in modInfo.Manifest.Sources)
            {
                var prefix = pattern;
                var suffix = "*.cs";
                var sepIndex = pattern.LastIndexOf('/');
                if (sepIndex >= 0)
                {
                    prefix = pattern.Substring(0, sepIndex);
                    suffix = pattern.Substring(sepIndex + 1);
                }

                CollectSourceFilesRecursive(modInfo.FileProvider, prefix, file =>
                {
                    if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        return false;

                    var fileName = file.Replace('\\', '/');
                    var fileNamePart = fileName.Substring(fileName.LastIndexOf('/') + 1);

                    return suffix == "*" || suffix == "*.cs" ||
                        (suffix.StartsWith("*.") && fileNamePart.EndsWith(suffix.Substring(1), StringComparison.OrdinalIgnoreCase));
                }, result);
            }

            if (result.Count == 0)
            {
                CollectSourceFilesRecursive(modInfo.FileProvider, "", file =>
                {
                    return file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                }, result);
            }

            return result;
        }

        private static void CollectSourceFilesRecursive(
            IModFileProvider provider,
            string directory,
            Func<string, bool> fileFilter,
            List<(string, string)> result)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectSourceFilesRecursive(provider, subDir, fileFilter, result);
                }
                else if (fileFilter(entry))
                {
                    var source = provider.ReadFileText(entry);
                    result.Add((entry, source));
                }
            }
        }

        private static List<ModInfo> TopologicalSort(List<ModInfo> modsWithCode)
        {
            var result = new List<ModInfo>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            void Visit(ModInfo mod)
            {
                if (visited.Contains(mod.Id))
                    return;
                if (visiting.Contains(mod.Id))
                {
                    GameLogger.Log("MODS_COMPILE", $"Circular dependency detected involving mod '{mod.Id}'", LogLevel.Warning);
                    return;
                }

                visiting.Add(mod.Id);

                if (mod.Manifest.Depends != null)
                {
                    foreach (var depId in mod.Manifest.Depends)
                    {
                        var dep = modsWithCode.Find(m => m.Id == depId);
                        if (dep != null)
                            Visit(dep);
                    }
                }

                visiting.Remove(mod.Id);
                visited.Add(mod.Id);
                result.Add(mod);
            }

            foreach (var mod in modsWithCode)
                Visit(mod);

            return result;
        }
        
        /// <summary>
        /// Compiles a set of source files into an in-memory assembly.
        /// First checks the SHA-256 output cache; skips compilation
        /// if a cached DLL exists and <c>UseAssemblyCache</c> is true.
        /// </summary>
        public static Assembly Compile(string modId, IEnumerable<(string FilePath, string Source)> sources)
        {
            var sourceList = sources.ToList();
            if (sourceList.Count == 0)
                return null;
            
            var hash = ComputeHash(sourceList);
            var cachedPath = Path.Combine(CacheRoot, $"{modId}_{hash}.dll");
            
            if (File.Exists(cachedPath))
            {
                if (ModConfig.Instance.UseAssemblyCache)
                {
                    GameLogger.Log("MODS_COMPILE", $"ModCompiler.Compile: loading cached {cachedPath}");
                    return ModAssemblyLoader.LoadFromBytes(File.ReadAllBytes(cachedPath));
                }
                
                File.Delete(cachedPath);
                GameLogger.Log("MODS_COMPILE", $"ModCompiler.Compile: cached {cachedPath} need delete by settings 'use_assembly_cache'", LogLevel.Warning);
            }

            var parseOptions = new CSharpParseOptions();
            var syntaxTrees = sourceList
                .Select(s => CSharpSyntaxTree.ParseText(s.Source, options: parseOptions, path: s.FilePath))
                .ToList();

            var metadataReferences = new List<MetadataReference>();
            AddCoreReferences(metadataReferences);

            var compilation = CSharpCompilation.Create(
                assemblyName: modId,
                syntaxTrees: syntaxTrees,
                references: metadataReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
            );

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(20).Select(d => $"{d.Location}: {d.GetMessage()}").ToList();
                GameLogger.CompilationErrors.Add($"Mod '{modId}' failed with {result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s):\n  " + string.Join("\n  ", errors));

                var errCount = 0;
                foreach (var d in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(20))
                {
                    GameLogger.Log("MODS_COMPILE", $"  ERR{++errCount}: {d.Location}: {d.GetMessage()}", LogLevel.Debug);
                }
                GameLogger.Log("MODS_COMPILE", $"ModCompiler.Compile: '{modId}' FAILED with {result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s)", LogLevel.Error);

                return null;
            }

            Directory.CreateDirectory(CacheRoot);
            ms.Seek(0, SeekOrigin.Begin);
            File.WriteAllBytes(cachedPath, ms.ToArray());
            
            GameLogger.Log("MODS_COMPILE", $"ModCompiler.Compile: Compilation of mod '{modId}' with {sourceList.Count} source files - success -> {cachedPath}");

            return ModAssemblyLoader.LoadFromBytes(ms.ToArray());
        }

        /// <summary>
        /// Compiles an arbitrary C# string into an assembly. Used by
        /// tools and the in-game script editor; does not check the
        /// output cache. Returns a <c>CompileResult</c> with errors
        /// on failure.
        /// </summary>
        public static CompileResult CompileString(string code, string scriptId)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code,
                options: new CSharpParseOptions(
                    preprocessorSymbols: new[]
                    {
                        "TOOLS", "DEBUG",
                        "LOG_MODS_COMPILE"
                    }),
                path: $"{scriptId}.cs");

            var references = new List<MetadataReference>();
            AddCoreReferences(references);

            var compilation = CSharpCompilation.Create(
                assemblyName: scriptId,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = new List<string>();
                foreach (var d in result.Diagnostics)
                {
                    if (d.Severity == DiagnosticSeverity.Error)
                        errors.Add(d.GetMessage());
                }
                return new CompileResult { Success = false, Errors = errors };
            }

            var assembly = ModAssemblyLoader.LoadFromBytes(ms.ToArray());
            return new CompileResult { Success = true, Assembly = assembly };
        }

        private static void AddCoreReferences(List<MetadataReference> references)
        {
            AddAssemblyReference(references, typeof(object).Assembly);

            var basePath = Path.GetDirectoryName(typeof(object).Assembly.Location);

            var requiredAssemblies = new[]
            {
                "System.Runtime.dll",
                "System.Collections.dll",
                "System.Linq.dll",
                "System.Text.Json.dll",
                "GodotSharp.dll",
            };

            foreach (var asmName in requiredAssemblies)
            {
                var path = Path.Combine(basePath, asmName);
                if (File.Exists(path))
                    references.Add(MetadataReference.CreateFromFile(path));
            }

            AddAssemblyReference(references, typeof(Godot.Node).Assembly);

            GameLogger.Log("MODS_COMPILE", "AddCoreReferences: scanning assembly directories for DLLs...");

            var searchedDirs = new HashSet<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                {
                    var dir = Path.GetDirectoryName(asm.Location);
                    if (!string.IsNullOrEmpty(dir) && searchedDirs.Add(dir))
                    {
                        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                            TryAddReference(references, dll);
                    }
                }
            }

            var buildOutputDir = ResolveBuildOutputDir();
            if (buildOutputDir != null && searchedDirs.Add(buildOutputDir))
            {
                GameLogger.Log("MODS_COMPILE", $"AddCoreReferences: scanning build output dir '{buildOutputDir}'");

                foreach (var dll in Directory.GetFiles(buildOutputDir, "*.dll"))
                    TryAddReference(references, dll);
            }

            if (searchedDirs.Add(CacheRoot))
            {
                GameLogger.Log("MODS_COMPILE", $"AddCoreReferences: scanning mod cache dir '{CacheRoot}'");

                foreach (var dll in Directory.GetFiles(CacheRoot, "*.dll"))
                    TryAddReference(references, dll);
            }

            var sdkAsm = typeof(Cthangover.Core.Actions.IScenarioAction).Assembly;
            var coreAsm = typeof(ModRegistry).Assembly;
            GameLogger.Log("MODS_COMPILE", $"AddCoreReferences: sdkAsm.Location='{sdkAsm.Location}' (empty={string.IsNullOrEmpty(sdkAsm.Location)})");
            GameLogger.Log("MODS_COMPILE", $"AddCoreReferences: coreAsm.Location='{coreAsm.Location}' (empty={string.IsNullOrEmpty(coreAsm.Location)})");
            GameLogger.Log("MODS_COMPILE", $"AddCoreReferences: total references={references.Count}");
            foreach (var r in references)
            {
                if(!r.Display?.Contains("mods/")??false)
                    continue;
                    GameLogger.Log("MODS_COMPILE", $"  ref: {r.Display}", LogLevel.Debug);
            }
        }

        private static string ResolveBuildOutputDir()
        {
            try
            {
                var projectRoot = ProjectSettings.GlobalizePath("res://");
                foreach (var config in new[] { "Debug", "Release" })
                {
                    var buildDir = Path.Combine(projectRoot, ".godot", "mono", "temp", "bin", config);
                    if (Directory.Exists(buildDir))
                        return buildDir;
                }
            }
            catch { }

            return null;
        }

        private static void TryAddReference(List<MetadataReference> references, string dllPath)
        {
            if (!File.Exists(dllPath))
                return;
            try
            {
                AssemblyName.GetAssemblyName(dllPath);
            }
            catch
            {
                return;
            }
            try
            {
                references.Add(MetadataReference.CreateFromFile(dllPath));
            }
            catch { }
        }

        private static void AddAssemblyReference(List<MetadataReference> references, Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                return;
            try
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            catch { }
        }

        private static string ComputeHash(List<(string FilePath, string Source)> sources)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder();
            foreach (var (path, source) in sources.OrderBy(s => s.FilePath))
            {
                sb.Append(path);
                sb.Append(':');
                sb.Append(source);
            }
            var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hashBytes).Substring(0, 16).ToLowerInvariant();
        }

        /// <summary>
        /// Deletes all cached compilation output for a specific mod.
        /// Returns true if any files were deleted.
        /// </summary>
        public static bool InvalidateCache(string modId)
        {
            if (!Directory.Exists(CacheRoot))
                return false;
            var deleted = false;
            foreach (var file in Directory.GetFiles(CacheRoot, $"{modId}_*.dll"))
            {
                File.Delete(file);
                deleted = true;
            }
            return deleted;
        }
    }
}
