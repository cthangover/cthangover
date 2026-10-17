using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;
using Godot;
using Microsoft.CodeAnalysis;

namespace Cthangover.Core.Mods.Compilation
{
    /// <summary>
    /// Assembly-reference discovery for mod compilations plus the warmed
    /// Godot path state the rest of the pipeline reads.
    ///
    /// <see cref="Collect"/> scans, in order: the runtime directories of
    /// every loaded assembly, the Godot build output directory, precompiled
    /// <c>mods/&lt;id&gt;/dll</c> folders and the compilation cache (newest
    /// artifact per known mod id). The scan is deliberately filtered — the
    /// inline comments explain why — because a naive directory scan would
    /// resurrect stale copies of mod assemblies and break dependent
    /// compilations with CS0433/CS0121.
    ///
    /// Path state (<see cref="WarmUpPaths"/>) must be warmed on the main
    /// thread before background compilation starts: compilation itself is
    /// pure .NET and thread-safe, but
    /// <c>ProjectSettings.GlobalizePath</c> is not allowed off the main
    /// thread, so resolved paths are cached here instead.
    /// </summary>
    public static class ModReferenceProvider
    {
        private static string _resRoot;
        private static string _buildOutputDir;
        private static bool _pathsWarmed;

        /// <summary>
        /// Absolute path to the Godot project root (<c>res://</c> globalized),
        /// or null outside runtime and before <see cref="WarmUpPaths"/>. The
        /// Godot source generator consumes it as
        /// <c>build_property.project_dir</c> to compute each generated
        /// class's <c>res://</c> script path relative to the project root.
        /// </summary>
        public static string ProjectRoot => _resRoot;

        /// <summary>
        /// Resolves the Godot project root and Mono build output directory once
        /// on the main thread. Compilation may run on a background thread during
        /// boot, where <c>ProjectSettings.GlobalizePath</c> is not allowed, so the
        /// resolved paths are cached here and reused by <see cref="Collect"/>.
        /// </summary>
        public static void WarmUpPaths()
        {
            if (_pathsWarmed)
                return;
            _pathsWarmed = true;

            _ = ModAssemblyCache.CacheRoot;

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

        /// <summary>
        /// Builds the metadata reference set for one mod compilation:
        /// the runtime and GodotSharp basics, every DLL next to a loaded
        /// assembly, the build output directory, precompiled mod DLLs and
        /// the compilation cache. <paramref name="currentModId"/> excludes
        /// the mod's own (possibly stale) cache artifacts from its own
        /// references; <paramref name="knownModIds"/>, when provided,
        /// restricts the cache scan to at most the newest DLL per real
        /// mod id, ignoring foreign helper artifacts.
        /// </summary>
        public static void Collect(List<MetadataReference> references, string currentModId = null, IReadOnlyCollection<string> knownModIds = null)
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

            // Assembly cache directories are handled by the dedicated filtered
            // scan below (newest DLL per known mod id). File-backed mod
            // assemblies (loaded via LoadFromPath for debugger symbols, §9.2)
            // expose those directories through Assembly.Location — adding them
            // here unfiltered would resurrect stale copies and compile-test
            // artifacts as references (CS0433/CS0121 in dependent mods).
            // CacheRoot keeps the trailing separator from GlobalizePath, while
            // Path.GetDirectoryName of a loaded mod assembly never returns one —
            // an untrimmed comparison would never match and the AppDomain scan
            // would resurrect every historical cache DLL as a reference
            // (CS0121 ambiguous extension calls in mods with moved types).
            var assemblyCacheDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ModAssemblyCache.CacheRoot));
            var testAssemblyCacheDir = Path.GetFullPath(Path.Combine(assemblyCacheDir, "..", "test_assemblies"));
            var searchedDirs = new HashSet<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                {
                    var dir = Path.GetFullPath(Path.GetDirectoryName(asm.Location) ?? string.Empty);
                    if (string.IsNullOrEmpty(dir))
                        continue;
                    if (dir.Equals(assemblyCacheDir, StringComparison.OrdinalIgnoreCase) ||
                        dir.Equals(testAssemblyCacheDir, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (searchedDirs.Add(dir))
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
                    // Precompiled libraries follow the same visibility rule as
                    // runtime-compiled mods: only the mod itself and its
                    // dependency closure may reference them.
                    var ownerId = Path.GetFileName(modDir);
                    if (knownModIds != null
                        && !string.Equals(ownerId, currentModId, StringComparison.OrdinalIgnoreCase)
                        && !knownModIds.Contains(ownerId, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var precompiledDir = Path.Combine(modDir, "dll");
                    if (!Directory.Exists(precompiledDir) || !searchedDirs.Add(precompiledDir))
                        continue;

                    foreach (var dll in Directory.GetFiles(precompiledDir, "*.dll"))
                        TryAddReference(references, dll);
                }
            }

            if (searchedDirs.Add(ModAssemblyCache.CacheRoot) && Directory.Exists(ModAssemblyCache.CacheRoot))
            {
                GameLogger.Log("MODS_COMPILE", $"AddCoreReferences: scanning mod cache dir '{ModAssemblyCache.CacheRoot}'");

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
                foreach (var dll in Directory.GetFiles(ModAssemblyCache.CacheRoot, "*.dll"))
                {
                    var fileName = Path.GetFileName(dll);

                    // A mod's own stale cached DLL (<modId>_<oldHash>.dll) must not
                    // become a metadata reference of the mod being compiled: after a
                    // source change the fresh assembly would collide with the stale
                    // one and fail with CS0121 (ambiguous extension method calls).
                    // The prefix check is deliberately broader than IsSelfCacheDll:
                    // helper artifacts (<id>_compile_test_<hash>.dll) share the
                    // prefix and must equally never self-reference.
                    if (currentModId != null && fileName.StartsWith(currentModId + "_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (newestById == null)
                    {
                        TryAddReference(references, dll);
                        continue;
                    }

                    if (!ModAssemblyCache.TryParseCacheDllName(fileName, out var ownerModId, out _)
                        || !knownModIds.Contains(ownerModId, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var modified = File.GetLastWriteTimeUtc(dll);
                    if (!newestById.TryGetValue(ownerModId, out var existing) || modified > existing.Modified)
                        newestById[ownerModId] = (dll, modified);
                }

                if (newestById != null)
                {
                    foreach (var entry in newestById)
                    {
                        // The artifact this process compiled the dependency to wins
                        // over the newest file (see ModAssemblyCache.CurrentArtifacts).
                        var path = ModAssemblyCache.CurrentArtifacts.TryGetValue(entry.Key, out var current) && File.Exists(current)
                            ? current
                            : entry.Value.Path;
                        TryAddReference(references, path);
                    }
                }
            }

            var sdkAsm = typeof(IScenarioAction).Assembly;
            var coreAsm = typeof(ModRegistry).Assembly;
            AddAssemblyReference(references, sdkAsm);
            AddAssemblyReference(references, coreAsm);
            if (knownModIds == null && references.Count > 300)
                Console.WriteLine($"REFSCAN-BIG[{currentModId}]: from:\n" + System.Environment.StackTrace);
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
        /// Absolute path for a mod source file when it exists on disk inside the
        /// project. The Godot source generator derives each class's script path
        /// (<c>res://...</c>) from the syntax tree's file path, so the tree must
        /// be parsed with its real location inside the project; otherwise the
        /// generated <c>[ScriptPath]</c> points nowhere and lifecycle callbacks
        /// never fire.
        /// </summary>
        public static string ResolveSourcePath(string modId, string filePath)
        {
            var resRoot = _resRoot;
            if (string.IsNullOrEmpty(resRoot))
                return filePath;

            var normalized = filePath.Replace('\\', '/');
            var absolute = Path.GetFullPath(Path.Combine(resRoot, "mods", modId, normalized));
            return File.Exists(absolute) ? absolute : filePath;
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
    }
}
