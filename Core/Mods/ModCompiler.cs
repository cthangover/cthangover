using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Cthangover.Core.Mods.Compilation;
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
    ///
    /// This class is the thin orchestrator of the pipeline; each step
    /// lives in its own type in
    /// <see cref="Cthangover.Core.Mods.Compilation"/>:
    /// <see cref="ModSourceCollector"/>, <see cref="ModCompileOrder"/>,
    /// <see cref="ModReferenceProvider"/>, <see cref="ModAssemblyCache"/>
    /// and <see cref="GodotGeneratorHost"/>.
    /// </summary>
    public static class ModCompiler
    {
        /// <summary>
        /// Absolute path to the assembly cache directory
        /// (<c>user://mod_cache/assemblies/</c>). Resolved once at
        /// static init time with a fallback to the OS temp folder.
        /// Storage and invalidation live in
        /// <see cref="ModAssemblyCache"/>; this wrapper keeps the
        /// historical <c>ModCompiler.CacheRoot</c> access path for the
        /// assembly loader and the mod test engine.
        /// </summary>
        public static string CacheRoot => ModAssemblyCache.CacheRoot;

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

            var sorted = ModCompileOrder.TopologicalSort(modsWithCode);
            var knownModIds = sorted.Select(modInfo => modInfo.Id).ToList();

            foreach (var modInfo in sorted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Fine-grained progress for the launcher boot bar: every mod is
                // one step of the Discovering phase, cache hits included.
                BootProgress.ReportSub(result.Count / (float)sorted.Count);

                try
                {
                    var sourceFiles = ModSourceCollector.CollectSourceFiles(modInfo, cancellationToken);
                    if (sourceFiles.Count == 0)
                        continue;

                    GameLogger.Log("MODS_COMPILE", $"ModCompiler.CompileAll: compiling mod '{modInfo.Id}' with {sourceFiles.Count} source file(s):");
                    foreach (var (path, _) in sourceFiles)
                        GameLogger.Log("MODS_COMPILE", $"  {path}", LogLevel.Debug);

                    var dependencySignature = ModAssemblyCache.BuildDependencySignature(modInfo, mods);
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

            var generators = GodotGeneratorHost.GetGodotGenerators();
            var cachedPath = ModAssemblyCache.BuildCachePath(modId, sourceList, dependencySignature, generators != null && generators.Length > 0);
            var cached = ModAssemblyCache.TryLoadCached(cachedPath, modId);
            if (cached != null)
                return cached;

            var parseOptions = new CSharpParseOptions();
            // Encoding is mandatory for PDB emission: source strings read from
            // mod providers carry no BOM, and Roslyn refuses to emit debug
            // information for trees without an explicit encoding.
            var syntaxTrees = sourceList
                .Select(s => CSharpSyntaxTree.ParseText(s.Source, options: parseOptions, path: ModReferenceProvider.ResolveSourcePath(modId, s.FilePath), encoding: System.Text.Encoding.UTF8, cancellationToken: cancellationToken))
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

            compilation = GodotGeneratorHost.RunGodotGenerators(generators, compilation, modId);

            cancellationToken.ThrowIfCancellationRequested();

            using var ms = new MemoryStream();
            // Portable PDB alongside the DLL: with the module file-backed on
            // disk, the debugger resolves symbols by naming convention and
            // stacks carry file:line (plan-mod-testing.md §9.2). Roslyn's
            // default EmitOptions already emit PortablePdb when a pdb stream
            // is provided.
            using var pdb = new MemoryStream();
            // The debug directory must name the PDB file actually written next
            // to the DLL: by default Roslyn records "<assemblyName>.pdb", but the
            // cache stores "<id>_<hash>.pdb", so neither the runtime (file:line
            // in stack traces) nor a debugger would find the symbols.
            var result = compilation.Emit(ms, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
                debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb,
                pdbFilePath: Path.ChangeExtension(cachedPath, ".pdb")));

            cancellationToken.ThrowIfCancellationRequested();

            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(20).Select(FormatDiagnostic).ToList();
                GameLogger.CompilationErrors.Add($"Mod '{modId}' failed with {result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s):\n  " + string.Join("\n  ", errors));

                var errCount = 0;
                foreach (var d in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(20))
                {
                    GameLogger.Log("MODS_COMPILE", $"  ERR{++errCount}: {FormatDiagnostic(d)}", LogLevel.Debug);
                }
                GameLogger.Log("MODS_COMPILE", $"ModCompiler.Compile: '{modId}' FAILED with {result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s)", LogLevel.Error);

                return null;
            }

            ModAssemblyCache.Store(cachedPath, ms, pdb);

            GameLogger.Log("MODS_COMPILE", $"ModCompiler.Compile: Compilation of mod '{modId}' with {sourceList.Count} source files - success -> {cachedPath}");

            // Disk load keeps the module file-backed so the adjacent PDB binds;
            // byte load stays the fallback (plan §9.2). The modId overload
            // routes the load into the mod's collectible context so a later
            // unload can release it.
            return ModAssemblyLoader.LoadFromPath(cachedPath, modId)
                ?? ModAssemblyLoader.LoadFromBytes(ms.ToArray());
        }

        /// <summary>
        /// Formats a Roslyn diagnostic the way IDEs and terminals link it:
        /// <c>path(line,col): error CSxxxx: message</c> with 1-based line and
        /// column, instead of the default character-span form
        /// <c>SourceFile(path[3904..3923))</c>.
        /// </summary>
        public static string FormatDiagnostic(Diagnostic d)
        {
            var severity = d.Severity.ToString().ToLowerInvariant();
            if (!d.Location.IsInSource)
                return $"{severity} {d.Id}: {d.GetMessage()}";
            var span = d.Location.GetLineSpan();
            var start = span.StartLinePosition;
            return $"{span.Path}({start.Line + 1},{start.Character + 1}): {severity} {d.Id}: {d.GetMessage()}";
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

        /// <summary>
        /// Delegating entry point kept on <see cref="ModCompiler"/> because
        /// the mod test engine resolves it by name via reflection
        /// (<c>GetMethod("AddCoreReferences", NonPublic | Static)</c>) to
        /// compile test assemblies against exactly the same reference set as
        /// production mods. The implementation lives in
        /// <see cref="ModReferenceProvider.Collect"/>; the parameter shape
        /// must stay reflection-callable.
        /// </summary>
        private static void AddCoreReferences(List<MetadataReference> references, string currentModId = null, IReadOnlyCollection<string> knownModIds = null)
        {
            ModReferenceProvider.Collect(references, currentModId, knownModIds);
        }

        /// <summary>
        /// Resolves the Godot project root and Mono build output directory once
        /// on the main thread. Compilation may run on a background thread during
        /// boot, where <c>ProjectSettings.GlobalizePath</c> is not allowed, so the
        /// resolved paths are cached and reused by the reference scan. Must be
        /// called before the first background compilation.
        /// </summary>
        public static void WarmUpPaths()
        {
            ModReferenceProvider.WarmUpPaths();
        }

        /// <summary>
        /// Deletes all cached compilation output for a specific mod.
        /// Returns true if any files were deleted.
        /// </summary>
        public static bool InvalidateCache(string modId)
        {
            return ModAssemblyCache.InvalidateCache(modId);
        }
    }
}
