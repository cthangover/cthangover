using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Facade of the mod unit-test system (plan §5). Owns the gate
    /// (<c>--run-tests</c> / <c>run_mod_tests</c>), the separate compilation
    /// of <c>test_sources</c> into <c>{modId}__tests</c> assemblies and the
    /// run orchestration.
    ///
    /// Gate guarantees (§3.1): when not activated, no member of this class is
    /// touched — production compilation never reads
    /// <see cref="ModManifest.TestSources"/>, test DLLs cache under a
    /// dedicated directory and never land in a player's installation, and no
    /// <c>__tests</c> assembly exists in the AppDomain after a normal boot.
    ///
    /// Orchestration (§5.1): called after the boot pipeline reaches Ready, it
    /// compiles each mod's tests (Roslyn diagnostics are reported verbatim on
    /// failure — the mod is marked ERROR and the run continues), discovers
    /// and runs the tests, writes the report and returns the aggregate exit
    /// flag the entry scene turns into <c>GetTree().Quit(0|1)</c>.
    /// </summary>
    public static class ModTestEngine
    {
        private static readonly ModTestLedger ledger = new();

        /// <summary>Roslyn errors of the last failed test compilation per mod, carried into the report's &lt;compile&gt; entry.</summary>
        private static readonly Dictionary<string, string> lastCompileErrors = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Results accumulated by the last (or current) run; kept for Report Viewer / diagnostics.</summary>
        public static ModTestLedger Ledger => ledger;

        /// <summary>True when <c>--run-tests</c> is present in the user command-line arguments.</summary>
        public static bool IsRequestedByArgs()
        {
            if (!GameEnvironment.IsRuntime)
                return false;
            return OS.GetCmdlineUserArgs().Any(a => a == "--run-tests" || a.StartsWith("--run-tests=", StringComparison.Ordinal));
        }

        /// <summary>Mod filter from <c>--mod=&lt;id&gt;</c> user args, or null when absent.</summary>
        public static string RequestedModFilter()
        {
            if (!GameEnvironment.IsRuntime)
                return null;
            return OS.GetCmdlineUserArgs()
                .Where(a => a.StartsWith("--mod=", StringComparison.Ordinal))
                .Select(a => a.Substring("--mod=".Length).Trim())
                .FirstOrDefault(id => !string.IsNullOrEmpty(id));
        }

        /// <summary>True when the test mode is active: explicit CLI flag, or run_mod_tests in mod_config.json.</summary>
        public static bool IsTestMode()
        {
            if (IsRequestedByArgs())
                return true;
            try
            {
                return ModConfig.Instance.RunModTests;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Compiles the mod's <c>test_sources</c> into the separate
        /// <c>{modId}__tests</c> assembly. Returns null on compilation failure
        /// (diagnostics already logged); never called by the production
        /// pipeline.
        /// </summary>
        public static Assembly CompileTestAssembly(ModInfo mod, CancellationToken cancellationToken)
        {
            var patterns = mod?.Manifest?.TestSources;
            if (patterns == null || patterns.Count == 0)
                return null;

            var sources = new List<(string FilePath, string Source)>();
            // Absolute on-disk path per source (same rule as production
            // mods): debuggers map breakpoints and stacks show the real file.
            var treePaths = new List<string>();
            foreach (var pattern in patterns)
            {
                var prefix = pattern;
                var suffix = "*.cs";
                var sepIndex = pattern.LastIndexOf('/');
                if (sepIndex >= 0)
                {
                    prefix = pattern.Substring(0, sepIndex);
                    suffix = pattern.Substring(sepIndex + 1);
                }

                foreach (var file in mod.FileProvider.ListFiles(prefix))
                {
                    if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var name = file.Substring(file.LastIndexOf('/') + 1);
                    var match = suffix == "*" || suffix == "*.cs" ||
                        (suffix.StartsWith("*.") && name.EndsWith(suffix.Substring(1), StringComparison.OrdinalIgnoreCase));
                    if (match)
                    {
                        // Deduplicate the pattern prefix: file paths already
                        // carry "tests/", and the report path keeps a single
                        // canonical "{modId}/" prefix.
                        var reportPath = file.StartsWith("tests/")
                            ? $"{mod.Id}/{file}"
                            : $"{mod.Id}/tests/{file}";
                        sources.Add((reportPath, mod.FileProvider.ReadFileText(file)));
                        treePaths.Add(Compilation.ModReferenceProvider.ResolveSourcePath(mod.Id, file, mod.FileProvider?.RootPath));
                    }
                }
            }

            if (sources.Count == 0)
                return null;

            // Separate cache root: a player's assembly cache never contains
            // test DLLs, and editing tests does not invalidate the production cache.
            var productionRoot = ModCompiler.CacheRoot;
            var testRoot = Path.Combine(Path.GetDirectoryName(productionRoot) ?? productionRoot, "test_assemblies");
            Directory.CreateDirectory(testRoot);
            var coreSignature = typeof(ModTestEngine).Assembly.ManifestModule.ModuleVersionId.ToString("N");
            // The production assemblies the tests link against are part of the
            // key: a changed mod API must recompile the tests (a compile error
            // with file:line) instead of reusing a stale DLL that fails at run
            // time with MissingMethodException.
            var hash = ModHasher.ComputeHash(sources, "tests;pdb=named;paths=abs;core=" + coreSignature + ";prod=" + ProductionSignature(mod));
            var cachedPath = Path.Combine(testRoot, $"{mod.Id}__tests_{hash}.dll");

            // Missing PDB = pre-§9.2 cache entry: recompile once to gain symbols.
            if (File.Exists(cachedPath) && File.Exists(Path.ChangeExtension(cachedPath, ".pdb")))
                return ModAssemblyLoader.LoadTestAssembly(cachedPath, mod.Id);

            // Encoding is mandatory for PDB emission (see ModCompiler.Compile).
            var syntaxTrees = sources
                .Select((s, i) => Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                    s.Source, path: treePaths[i], encoding: System.Text.Encoding.UTF8, cancellationToken: cancellationToken))
                .ToList();

            var references = new List<Microsoft.CodeAnalysis.MetadataReference>();
            // Known mod ids make the reference scan pick at most one (newest)
            // DLL per mod from the production cache: stale cooking_<oldhash>.dll
            // copies would otherwise collide with the live one (CS0433).
            var knownModIds = ModManager.Instance.Mods.Keys.ToList();
            var refMethod = typeof(ModCompiler).GetMethod("AddCoreReferences",
                BindingFlags.NonPublic | BindingFlags.Static);
            // currentModId must NOT be the mod under test here: the self-cache
            // skip would exclude the mod's own production assembly from the
            // references — exactly the assembly the tests must link against.
            // No other mod may reference its own stale DLL either, so pass a
            // sentinel id that matches no cache artifact.
            refMethod?.Invoke(null, new object[] { references, "$__tests__", knownModIds });

            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                assemblyName: $"{mod.Id}__tests",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                        Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Debug));

            using var ms = new MemoryStream();
            // Portable PDB next to the test DLL: file-backed load + adjacent
            // symbols give the debugger line-level breakpoints inside mod test
            // code (plan-mod-testing.md §9.2).
            using var pdb = new MemoryStream();
            // Name the cache-file PDB in the debug directory (see ModCompiler.Compile).
            var result = compilation.Emit(ms, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
                debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb,
                pdbFilePath: Path.ChangeExtension(cachedPath, ".pdb")));
            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .Take(20)
                    .Select(ModCompiler.FormatDiagnostic)
                    .ToList();
                GameLogger.CompilationErrors.Add($"Test assembly '{mod.Id}__tests' failed with {errors.Count} error(s):\n  " + string.Join("\n  ", errors));
                GameLogger.Log("MODTEST", $"ModTestEngine: test compilation of '{mod.Id}' FAILED with {errors.Count} error(s)", LogLevel.Error);
                foreach (var e in errors)
                    GameLogger.Log("MODTEST", $"  {e}", LogLevel.Warning);
                lastCompileErrors[mod.Id] = string.Join("\n", errors);
                return null;
            }

            File.WriteAllBytes(cachedPath, ms.ToArray());
            var cachedPdbPath = Path.ChangeExtension(cachedPath, ".pdb");
            pdb.Seek(0, SeekOrigin.Begin);
            File.WriteAllBytes(cachedPdbPath, pdb.ToArray());
            GameLogger.Log("MODTEST", $"ModTestEngine: compiled test assembly '{mod.Id}__tests' ({sources.Count} files) -> {cachedPath}");
            return ModAssemblyLoader.LoadTestAssembly(cachedPath, mod.Id);
        }

        /// <summary>
        /// Identity of the production assemblies a mod's tests compile
        /// against: module version ids of the mod's own assembly and of its
        /// transitive dependencies, as currently loaded. A recompiled mod gets
        /// a new MVID, so its tests recompile too; a cache hit keeps the MVID.
        /// Mods without code contribute nothing.
        /// </summary>
        private static string ProductionSignature(ModInfo mod)
        {
            var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(mod.Id);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!ids.Add(id))
                    continue;
                if (ModManager.Instance.GetMod(id) is ModInfo info && info.Manifest?.DependencyIds != null)
                    foreach (var dep in info.Manifest.DependencyIds)
                        pending.Push(dep);
            }

            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .GroupBy(a => a.GetName().Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
            return string.Join(";", ids
                .Where(loaded.ContainsKey)
                .Select(id => id + "=" + loaded[id].ManifestModule.ModuleVersionId.ToString("N")));
        }

        /// <summary>
        /// Runs the whole test mode: compiles and runs tests for every
        /// eligible mod (optionally filtered by <paramref name="modFilter"/>),
        /// writes the report through <see cref="ModTestReportWriter"/> and
        /// returns the aggregate pass flag. Compile failures and
        /// <c>mod_test_exclude</c> entries do not abort the run (§5.1).
        /// </summary>
        public static async Task<bool> RunAllAsync(string modFilter, CancellationToken cancellationToken)
        {
            ledger.Clear();
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var id in ModConfig.Instance.ModTestExclude ?? new List<string>())
                    excluded.Add(id);
            }
            catch { /* config not loaded — no exclusions */ }

            var eligible = ModManager.Instance.Mods.Values
                .OfType<ModInfo>()
                .Where(m => m.Manifest?.TestSources != null && m.Manifest.TestSources.Count > 0)
                .Where(m => modFilter == null || string.Equals(m.Id, modFilter, StringComparison.OrdinalIgnoreCase))
                .Where(m => !excluded.Contains(m.Id))
                .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (eligible.Count == 0)
            {
                // An explicit --mod= that matches nothing is a mistake (typo,
                // mod disabled, test_sources missing), not a green run.
                if (modFilter != null)
                {
                    GameLogger.Log("MODTEST", $"ModTestEngine: --mod={modFilter} matched no enabled mod with test_sources (excluded: {(excluded.Count == 0 ? "none" : string.Join(", ", excluded))})", LogLevel.Error);
                    GameLogger.Log("MODTEST", $"[TEST_RESULT] name=mod_units passed=0 failed=1");
                    return false;
                }
                GameLogger.Log("MODTEST", "ModTestEngine: no mods with test_sources found");
                return true;
            }

            GameLogger.Log("MODTEST", $"ModTestEngine: running mod tests, mode=author, mods: {string.Join(", ", eligible.Select(m => m.Id))}");

            await ModTestRunner.CaptureRootBaselineAsync();

            var summaries = new List<ModTestSuiteSummary>();
            var aborted = false;
            try
            {
                foreach (var mod in eligible)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Assembly testAssembly = null;
                    lastCompileErrors.Remove(mod.Id);
                    try
                    {
                        testAssembly = CompileTestAssembly(mod, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("MODTEST", $"compile of '{mod.Id}__tests' threw: {ex.Message}", LogLevel.Error);
                    }

                    lastCompileErrors.TryGetValue(mod.Id, out var compileErrors);
                    var summary = await ModTestRunner.RunSuiteAsync(mod.Id, testAssembly, ledger, cancellationToken, compileErrors);
                    summaries.Add(summary);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Keep what already ran: a partial report is the debugging
                // material for why the run was aborted.
                aborted = true;
                GameLogger.Log("MODTEST", $"ModTestEngine: run aborted after {summaries.Count}/{eligible.Count} suite(s); writing partial report", LogLevel.Error);
            }

            var success = ModTestReportWriter.Write(summaries, "author");
            return success && !aborted;
        }
    }
}
