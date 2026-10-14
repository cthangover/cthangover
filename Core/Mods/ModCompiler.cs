using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Cthangover.Core.Actions;
using Godot;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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
            if (GameEnvironment.IsRuntime)
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
            else
            {
                CacheRoot = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "cthangover", "mod_cache", "assemblies");
            }
        }

        /// <summary>
        /// Compiles all mods that declare source files in their manifest and
        /// returns the resulting assemblies WITHOUT registering them. Mods are
        /// sorted topologically by dependencies before compilation; results are
        /// cached on disk by SHA-256 content hash.
        ///
        /// This is the pure-.NET phase of mod loading — it only does Roslyn
        /// compilation and file I/O, so it can run on a background thread during
        /// boot. Registration into the game's registries (which may instantiate
        /// Godot types) must happen later on the main thread via
        /// <see cref="ModAssemblyLoader.RegisterAssembly"/> and
        /// <see cref="ScriptTypeRegistry.RegisterAssembly"/>.
        /// </summary>
        /// <returns>A list of <c>(modId, assembly, sourceFiles)</c> tuples for
        /// every successfully compiled mod, in dependency order.</returns>
        public static List<(string ModId, Assembly Assembly, List<(string FilePath, string Source)> Sources)> CompileAll(IDictionary<string, IModInfo> mods, CancellationToken cancellationToken = default)
        {
            var result = new List<(string ModId, Assembly Assembly, List<(string FilePath, string Source)> Sources)>();

            cancellationToken.ThrowIfCancellationRequested();

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
                return result;

            var sorted = TopologicalSort(modsWithCode);
            var knownModIds = sorted.Select(modInfo => modInfo.Id).ToList();

            foreach (var modInfo in sorted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Fine-grained progress for the launcher boot bar: every mod is
                // one step of the Discovering phase, cache hits included.
                BootProgress.ReportSub(result.Count / (float)sorted.Count);

                try
                {
                    var sourceFiles = CollectSourceFiles(modInfo, cancellationToken);
                    if (sourceFiles.Count == 0)
                        continue;

                    GameLogger.Log("MODS_COMPILE", $"ModCompiler.CompileAll: compiling mod '{modInfo.Id}' with {sourceFiles.Count} source file(s):");
                    foreach (var (path, _) in sourceFiles)
                        GameLogger.Log("MODS_COMPILE", $"  {path}", LogLevel.Debug);

                    var dependencySignature = BuildDependencySignature(modInfo, mods);
                    if (!string.IsNullOrEmpty(dependencySignature))
                        GameLogger.Log("MODS_COMPILE", $"  dependencies: {dependencySignature}", LogLevel.Debug);

                    var assembly = Compile(modInfo.Id, sourceFiles, dependencySignature, cancellationToken, knownModIds);
                    if (assembly != null)
                    {
                        result.Add((modInfo.Id, assembly, sourceFiles));
                        GameLogger.Log("MODS_COMPILE", $"ModCompiler.CompileAll: Compiled mod '{modInfo.Id}', {sourceFiles.Count} source file(s)");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    GameLogger.CompilationErrors.Add($"Mod '{modInfo.Id}': {ex.Message}");
                    GameLogger.Log("MODS_COMPILE", $"ModCompiler.CompileAll: Failed to compile mod '{modInfo.Id}': {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a deterministic signature of a mod's dependencies for the
        /// compilation cache key: every dependency id together with the version that
        /// was actually loaded (or <c>missing</c>). Renaming or upgrading a dependency
        /// therefore forces dependents to recompile against the new assembly instead
        /// of loading a stale cached DLL that links to the old assembly name.
        /// </summary>
        private static string BuildDependencySignature(ModInfo modInfo, IDictionary<string, IModInfo> mods)
        {
            var dependencies = modInfo?.Manifest?.DependencyIds;
            if (dependencies == null || dependencies.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            foreach (var depId in dependencies.Distinct().OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                var version = mods != null && mods.TryGetValue(depId, out var dependency) ? dependency.Version : null;
                parts.Add($"{depId}@{version ?? "missing"}");
            }

            return string.Join(";", parts);
        }

        private static List<(string FilePath, string Source)> CollectSourceFiles(ModInfo modInfo, CancellationToken cancellationToken)
        {
            var result = new List<(string, string)>();

            foreach (var pattern in modInfo.Manifest.Sources)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                }, result, cancellationToken);
            }

            if (result.Count == 0)
            {
                CollectSourceFilesRecursive(modInfo.FileProvider, "", file =>
                {
                    return file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                }, result, cancellationToken);
            }

            return result;
        }

        private static void CollectSourceFilesRecursive(
            IModFileProvider provider,
            string directory,
            Func<string, bool> fileFilter,
            List<(string, string)> result,
            CancellationToken cancellationToken)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectSourceFilesRecursive(provider, subDir, fileFilter, result, cancellationToken);
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

                if (mod.Manifest.DependencyIds != null)
                {
                    foreach (var depId in mod.Manifest.DependencyIds)
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
        public static Assembly Compile(string modId, IEnumerable<(string FilePath, string Source)> sources, CancellationToken cancellationToken = default)
        {
            return Compile(modId, sources, null, cancellationToken);
        }

        /// <summary>
        /// Same as <see cref="Compile(string, IEnumerable{(string FilePath, string Source)}, CancellationToken)"/>,
        /// but mixes <paramref name="dependencySignature"/> into the cache key. The
        /// signature describes the mod's dependencies (id + resolved version), so a
        /// renamed or version-bumped dependency invalidates the cached assembly even
        /// when this mod's own sources did not change. Without it, a cached DLL keeps
        /// linking against the old dependency assembly name and fails to load.
        /// When <paramref name="knownModIds"/> is provided (production CompileAll does
        /// this), the reference scan of the compilation cache references at most one
        /// DLL per known mod id and ignores artifacts of unknown origin
        /// (e.g. <c>&lt;id&gt;_compile_test_&lt;hash&gt;.dll</c> from the compile guard).
        /// </summary>
        public static Assembly Compile(string modId, IEnumerable<(string FilePath, string Source)> sources, string dependencySignature, CancellationToken cancellationToken = default, IReadOnlyCollection<string> knownModIds = null)
        {
            var sourceList = sources.ToList();
            if (sourceList.Count == 0)
                return null;

            cancellationToken.ThrowIfCancellationRequested();

            // Generated Godot script bindings change the assembly layout; the
            // marker keeps cached DLLs without them from being reused. The marker
            // also records whether generators were available: a cache entry built
            // without them must never be reused once they become available.
            var generators = GetGodotGenerators();
            var codegenMarker = generators != null && generators.Length > 0
                ? "godot-scriptpath"
                : "legacy-no-generators";
            // Mix the core assembly identity into the cache key: when the kernel
            // itself changes (e.g. a subsystem type moved out of Core), mods compiled
            // against the previous core keep stale IL references to removed types and
            // would fail at runtime with TypeLoadException. A new core MVID forces
            // every mod to recompile against the updated kernel.
            var coreSignature = typeof(ModRegistry).Assembly.ManifestModule.ModuleVersionId.ToString("N");
            var hash = ModHasher.ComputeHash(sourceList, (dependencySignature ?? string.Empty) + ";core=" + coreSignature + ";codegen=" + codegenMarker);
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
                .Select(s => CSharpSyntaxTree.ParseText(s.Source, options: parseOptions, path: ResolveSourcePath(modId, s.FilePath), cancellationToken: cancellationToken))
                .ToList();

            var metadataReferences = new List<MetadataReference>();
            AddCoreReferences(metadataReferences, modId, knownModIds);

            var compilation = CSharpCompilation.Create(
                assemblyName: modId,
                syntaxTrees: syntaxTrees,
                references: metadataReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
            );

            compilation = RunGodotGenerators(generators, compilation, modId);

            cancellationToken.ThrowIfCancellationRequested();

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            cancellationToken.ThrowIfCancellationRequested();

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

        private static void AddCoreReferences(List<MetadataReference> references, string currentModId = null, IReadOnlyCollection<string> knownModIds = null)
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

            // Precompiled library mods (mods/<id>/dll/*.dll) are loaded from
            // bytes at runtime, so their Assembly.Location is empty and the
            // AppDomain scan above never sees their directories. Reference
            // them explicitly so runtime-compiled dependants keep compiling
            // (live2d_test needs the live2d library types).
            var modsRoot = string.IsNullOrEmpty(_resRoot) ? null : Path.Combine(_resRoot, "mods");
            if (modsRoot != null && Directory.Exists(modsRoot) && searchedDirs.Add(modsRoot))
            {
                foreach (var modDir in Directory.GetDirectories(modsRoot))
                {
                    var precompiledDir = Path.Combine(modDir, "dll");
                    if (!Directory.Exists(precompiledDir) || !searchedDirs.Add(precompiledDir))
                        continue;

                    foreach (var dll in Directory.GetFiles(precompiledDir, "*.dll"))
                        TryAddReference(references, dll);
                }
            }

            if (searchedDirs.Add(CacheRoot) && Directory.Exists(CacheRoot))
            {
                GameLogger.Log("MODS_COMPILE", $"AddCoreReferences: scanning mod cache dir '{CacheRoot}'");

                // The cache legitimately holds several <id>_<hash>.dll copies per
                // mod (every source change leaves the previous hash behind), and
                // test runners drop helper artifacts there too
                // (<id>_compile_test_<hash>.dll). Referencing more than one copy of
                // the same mod's assembly would make every dependent compilation
                // fail with CS0433 (the type exists in both assemblies), so when
                // the caller knows the real mod ids only the newest artifact of
                // each known mod is referenced and everything else is ignored.
                var newestById = knownModIds != null
                    ? new Dictionary<string, (string Path, DateTime Modified)>(StringComparer.OrdinalIgnoreCase)
                    : null;
                foreach (var dll in Directory.GetFiles(CacheRoot, "*.dll"))
                {
                    var fileName = Path.GetFileName(dll);

                    // A mod's own stale cached DLL (<modId>_<oldHash>.dll) must not
                    // become a metadata reference of the mod being compiled: after a
                    // source change the fresh assembly would collide with the stale
                    // one and fail with CS0121 (ambiguous extension method calls).
                    if (currentModId != null && IsSelfCacheDll(fileName, currentModId))
                        continue;

                    if (newestById == null)
                    {
                        TryAddReference(references, dll);
                        continue;
                    }

                    if (!TryParseCacheDllName(fileName, out var ownerModId, out _)
                        || !knownModIds.Contains(ownerModId, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var modified = File.GetLastWriteTimeUtc(dll);
                    if (!newestById.TryGetValue(ownerModId, out var existing) || modified > existing.Modified)
                        newestById[ownerModId] = (dll, modified);
                }

                if (newestById != null)
                {
                    foreach (var entry in newestById.Values)
                        TryAddReference(references, entry.Path);
                }
            }

            var sdkAsm = typeof(IScenarioAction).Assembly;
            var coreAsm = typeof(ModRegistry).Assembly;
            AddAssemblyReference(references, sdkAsm);
            AddAssemblyReference(references, coreAsm);
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

        /// <summary>
        /// True when <paramref name="fileName"/> is a cached assembly of
        /// <paramref name="modId"/> itself, i.e. named <c>&lt;modId&gt;_&lt;hash&gt;.dll</c>
        /// where <c>&lt;hash&gt;</c> is the hex compilation-cache key (see <see cref="ModHasher.ComputeHash"/>).
        /// </summary>
        private static bool IsSelfCacheDll(string fileName, string modId)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return false;

            var name = fileName.Substring(0, fileName.Length - 4);
            if (!name.StartsWith(modId + "_", StringComparison.OrdinalIgnoreCase))
                return false;

            var hash = name.Substring(modId.Length + 1);
            return hash.Length > 0 && hash.All(char.IsAsciiHexDigit);
        }

        /// <summary>
        /// Parses a cache artifact name of the shape <c>&lt;modId&gt;_&lt;hash&gt;.dll</c>.
        /// The mod id itself may contain underscores, so the split happens at the
        /// last one; the hash must be all hex digits (see <see cref="ModHasher.ComputeHash"/>).
        /// Helper artifacts such as <c>tools_compile_test_&lt;hash&gt;.dll</c> parse to an
        /// id that is not a real mod, which keeps them out of the reference scan
        /// when the caller passes the known mod id set.
        /// </summary>
        private static bool TryParseCacheDllName(string fileName, out string modId, out string hash)
        {
            modId = null;
            hash = null;
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return false;

            var name = fileName.Substring(0, fileName.Length - 4);
            var separatorIndex = name.LastIndexOf('_');
            if (separatorIndex <= 0 || separatorIndex >= name.Length - 1)
                return false;

            var idPart = name.Substring(0, separatorIndex);
            var hashPart = name.Substring(separatorIndex + 1);
            if (!hashPart.All(char.IsAsciiHexDigit))
                return false;

            modId = idPart;
            hash = hashPart;
            return true;
        }

        private static string _resRoot;
        private static string _buildOutputDir;
        private static bool _pathsWarmed;

        /// <summary>
        /// Resolves the Godot project root and Mono build output directory once
        /// on the main thread. Compilation may run on a background thread during
        /// boot, where <c>ProjectSettings.GlobalizePath</c> is not allowed, so the
        /// resolved paths are cached here and reused by <see cref="AddCoreReferences"/>.
        /// </summary>
        public static void WarmUpPaths()
        {
            if (_pathsWarmed)
                return;
            _pathsWarmed = true;

            _ = CacheRoot;

            if (!GameEnvironment.IsRuntime)
                return;

            try
            {
                _resRoot = ProjectSettings.GlobalizePath("res://");
                foreach (var config in new[] { "Debug", "Release" })
                {
                    var buildDir = Path.Combine(_resRoot, ".godot", "mono", "temp", "bin", config);
                    if (Directory.Exists(buildDir))
                    {
                        _buildOutputDir = buildDir;
                        break;
                    }
                }
            }
            catch { }
        }

        private static string ResolveBuildOutputDir()
        {
            if (!GameEnvironment.IsRuntime)
                return null;

            if (_resRoot == null)
                return null;

            if (_buildOutputDir == null)
                return null;

            return _buildOutputDir;
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

        private static readonly object _generatorLock = new object();
        private static Type[] _godotGeneratorTypes;

        /// <summary>
        /// Absolute path for a mod source file when it exists on disk inside the
        /// project. The Godot source generator derives each class's script path
        /// (<c>res://...</c>) from the syntax tree's file path, so the tree must
        /// be parsed with its real location inside the project; otherwise the
        /// generated <c>[ScriptPath]</c> points nowhere and lifecycle callbacks
        /// never fire.
        /// </summary>
        private static string ResolveSourcePath(string modId, string filePath)
        {
            var resRoot = _resRoot;
            if (string.IsNullOrEmpty(resRoot))
                return filePath;

            var normalized = filePath.Replace('\\', '/');
            var absolute = Path.GetFullPath(Path.Combine(resRoot, "mods", modId, normalized));
            return File.Exists(absolute) ? absolute : filePath;
        }

        /// <summary>
        /// Locates the Godot source generator assembly shipped with the
        /// <c>godot.sourcegenerators</c> NuGet package (or copied next to the
        /// game binary).
        /// </summary>
        private static string ResolveGeneratorDll()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "Godot.SourceGenerators.dll"),
            };

            var packagesRoot = System.Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (string.IsNullOrEmpty(packagesRoot))
                packagesRoot = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages");
            var sgRoot = Path.Combine(packagesRoot, "godot.sourcegenerators");
            try
            {
                if (Directory.Exists(sgRoot))
                {
                    foreach (var version in Directory.GetDirectories(sgRoot).OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase))
                        candidates.Add(Path.Combine(version, "analyzers", "dotnet", "cs", "Godot.SourceGenerators.dll"));
                }
            }
            catch { }

            return candidates.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Creates FRESH generator instances for one compilation. The Godot
        /// generators are single-shot: a reused <c>ISourceGenerator</c>
        /// instance silently stops emitting (notably
        /// <c>ScriptPathAttributeGenerator</c>) on every driver run after the
        /// first one, so only the type list is cached.
        /// </summary>
        private static Microsoft.CodeAnalysis.ISourceGenerator[] GetGodotGenerators()
        {
            lock (_generatorLock)
            {
                if (_godotGeneratorTypes == null)
                {
                    InstallRoslynAssemblyResolver();
                    string dllPath = null;
                    try
                    {
                        dllPath = ResolveGeneratorDll();
                        if (string.IsNullOrEmpty(dllPath))
                        {
                            GameLogger.Log("MODS_COMPILE", "ModCompiler: Godot.SourceGenerators.dll not found, mod scripts will lack lifecycle binding", LogLevel.Warning);
                            _godotGeneratorTypes = Array.Empty<Type>();
                            return Array.Empty<Microsoft.CodeAnalysis.ISourceGenerator>();
                        }

                        var generatorAssembly = Assembly.LoadFrom(dllPath);
                        _godotGeneratorTypes = generatorAssembly.GetTypes()
                            .Where(t => !t.IsAbstract && typeof(Microsoft.CodeAnalysis.ISourceGenerator).IsAssignableFrom(t))
                            .ToArray();
                        GameLogger.Log("MODS_COMPILE", $"ModCompiler: Godot source generators loaded from '{dllPath}' ({_godotGeneratorTypes.Length} generator(s))");
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        var details = rtle.LoaderExceptions != null && rtle.LoaderExceptions.Length > 0
                            ? string.Join("\n  ", rtle.LoaderExceptions.Take(5).Select(le => le.GetType().FullName + ": " + le.Message))
                            : "(no loader exceptions) " + rtle + "\n  at " + rtle.StackTrace;
                        GameLogger.Log("MODS_COMPILE", $"ModCompiler: failed to load Godot source generators (dll '{dllPath}'):\n  {details}", LogLevel.Warning);
                        _godotGeneratorTypes = Array.Empty<Type>();
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("MODS_COMPILE", $"ModCompiler: failed to load Godot source generators (dll '{dllPath}'): {ex.GetType().FullName}: {ex.Message}\n  at {ex.StackTrace}", LogLevel.Warning);
                        _godotGeneratorTypes = Array.Empty<Type>();
                    }
                }

                return _godotGeneratorTypes
                    .Select(t => (Microsoft.CodeAnalysis.ISourceGenerator)Activator.CreateInstance(t, nonPublic: true))
                    .ToArray();
            }
        }

/// <summary>
        /// The generator assembly is built against an older Microsoft.CodeAnalysis
        /// than the one the game ships. When the runtime fails to bind those
        /// references by version, redirect them to the already-loaded instances -
        /// CLR type identity then unifies and the generators run against the
        /// game's Roslyn.
        /// </summary>
        private static void InstallRoslynAssemblyResolver()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var name = new AssemblyName(args.Name).Name;
                if (name == null || !name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                    return null;

                var loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == name);
                if (loaded != null)
                    return loaded;

                var local = Path.Combine(AppContext.BaseDirectory, name + ".dll");
                return File.Exists(local) ? Assembly.LoadFrom(local) : null;
            };
        }

        /// <summary>
        /// Runs the official Godot source generators over the mod compilation so
        /// every Godot-derived class gets <c>[ScriptPath]</c> (plus the assembly
        /// marker), exactly like the main project build. Without this, engine
        /// lifecycle callbacks are never dispatched for mod nodes.
        /// </summary>
        private static CSharpCompilation RunGodotGenerators(Microsoft.CodeAnalysis.ISourceGenerator[] generators, CSharpCompilation compilation, string modId)
        {
            if (generators == null || generators.Length == 0)
                return compilation;

            try
            {
                var driver = CSharpGeneratorDriver.Create(
                    generators,
                    null,
                    null,
                    new ModAnalyzerConfigOptionsProvider(_resRoot));
                driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

                foreach (var d in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(10))
                    GameLogger.Log("MODS_COMPILE", $"Godot generator ({modId}): {d.GetMessage()}", LogLevel.Warning);

                var genResult = (CSharpCompilation)output;
                var newTrees = genResult.SyntaxTrees.Count() - compilation.SyntaxTrees.Count();
                GameLogger.Log("MODS_COMPILE", $"Godot generator ({modId}): {newTrees} generated tree(s)", LogLevel.Debug);
                foreach (var tree in genResult.SyntaxTrees.Skip(compilation.SyntaxTrees.Count()).Take(40))
                    GameLogger.Log("MODS_COMPILE", $"Godot generator ({modId}): + '{tree.FilePath}'");

                return genResult;
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_COMPILE", $"RunGodotGenerators('{modId}') failed: {ex.Message}\n{ex.StackTrace}", LogLevel.Warning);
                return compilation;
            }
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

    /// <summary>
    /// Supplies <c>build_property.project_dir</c> to the Godot source generator
    /// so it computes script paths relative to the project root exactly like the
    /// main <c>dotnet build</c> does.
    /// </summary>
    internal sealed class ModAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly string _projectDir;

        public ModAnalyzerConfigOptions(string projectDir) => _projectDir = projectDir ?? string.Empty;

        public override bool TryGetValue(string key, out string value)
        {
            // Case-insensitive on purpose: MSBuild preserves the property name's
            // casing in "build_property.<Name>" keys, and the Godot generators
            // ask for 'GodotProjectDir'/'GodotProjectDirBase64' exactly as the
            // Sdk spells them. Every key below maps to a real Sdk property.
            switch (key?.ToLowerInvariant())
            {
                case "build_property.project_dir":
                case "build_property.project_directory":
                case "build_property.godotprojectdir":
                    value = _projectDir;
                    return true;

                case "build_property.godotprojectdirbase64":
                    value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_projectDir));
                    return true;

                case "build_property.isgodottoolsproject":
                    // The Sdk leaves this property empty for the game project
                    // itself; it is 'true' only for the editor tool assembly.
                    // ScriptPathAttributeGenerator and GodotPluginsInitializer
                    // bail out when it is 'true', which would strip
                    // [ScriptPathAttribute] from runtime-compiled mods and kill
                    // their [Signal]s, so mirror the game project here.
                    value = string.Empty;
                    return true;

                case "build_property.godotsourcegenerators":
                case "build_property.godotdisabledsourcegenerators":
                    // No generators disabled.
                    value = string.Empty;
                    return true;
            }

            value = null;
            return false;
        }
    }

    internal sealed class ModAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly ModAnalyzerConfigOptions _options;

        public ModAnalyzerConfigOptionsProvider(string projectDir) => _options = new ModAnalyzerConfigOptions(projectDir);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }
}
