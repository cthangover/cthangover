namespace Cthangover.Core.Mods.Compilation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Cthangover.Core.Actions;
    using Cthangover.Core.Utils;
    using Godot;
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// Assembly-reference discovery for mod compilations plus the warmed
    /// Godot path state the rest of the pipeline reads.
    ///
    /// <see cref="Collect"/> scans, in order: the runtime directories of
    /// every loaded assembly, the Godot build output directory, precompiled
    /// selected providers' <c>dll/</c> folders, declared managed references and the compilation cache (newest
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
        private static volatile bool _pathsWarmed;

        /// <summary>Godot editor output configurations searched when the core has no assembly location.</summary>
        internal static IReadOnlyList<string> BuildConfigurations { get; } = Array.AsReadOnly(new[] { "Debug", "Release" });

        /// <summary>Indicates that main-thread path resolution has completed.</summary>
        internal static bool PathsWarmed => _pathsWarmed;

        /// <summary>
        /// Absolute path to the Godot project root (<c>res://</c> globalized),
        /// or null outside runtime and before <see cref="WarmUpPaths"/>. The
        /// Godot source generator consumes it as
        /// <c>build_property.project_dir</c> to compute each generated
        /// class's <c>res://</c> script path relative to the project root.
        /// </summary>
        public static string ProjectRoot => _resRoot;

        /// <summary>Physical core output resolved on the main thread when Godot byte-loads its assembly.</summary>
        public static string BuildOutputDirectory => _pathsWarmed && GameEnvironment.IsRuntime
            ? CoreAssemblyContract.ImplementationDirectory ?? _buildOutputDir
            : _buildOutputDir;

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

            _ = ModAssemblyCache.CacheRoot;

            if (!GameEnvironment.IsRuntime)
            {
                _pathsWarmed = true;
                return;
            }

            try
            {
                _resRoot = GamePaths.Globalize("res://");
                foreach (var config in BuildConfigurations)
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
            _pathsWarmed = true;
        }

        /// <summary>
        /// Builds the metadata reference set for one mod compilation:
        /// the shipped net10.0 reference pack and GodotSharp, non-framework DLLs next to a loaded
        /// assembly, the build output directory, precompiled mod DLLs and
        /// the compilation cache. <paramref name="currentModId"/> excludes
        /// the mod's own (possibly stale) cache artifacts from its own
        /// references; <paramref name="knownModIds"/>, when provided,
        /// restricts the cache scan to at most the newest DLL per real
        /// mod id, ignoring foreign helper artifacts. <paramref name="referenceMods"/>
        /// is the selected mod set when compiling at boot or preparing a reload; its
        /// providers identify the winning installation across all roots.
        /// </summary>
        public static void Collect(List<MetadataReference> references, string currentModId = null, IReadOnlyCollection<string> knownModIds = null, IEnumerable<IModInfo> referenceMods = null)
        {
            foreach (var path in FrameworkReferencePack.AssemblyPaths)
                references.Add(MetadataReference.CreateFromFile(path));

            if (CoreAssemblyContract.Reference is { } coreReference)
                references.Add(coreReference);
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

            // Byte-loaded libraries have no Assembly.Location. Production callers
            // pass their selected providers (also during reload preparation), so a
            // losing copy in another root never contributes a conflicting DLL.
            // Standalone compile helpers without discovery still search every root.
            var modDirectories = referenceMods != null
                ? referenceMods.Select(mod => (Id: mod.Id, Path: mod.FileProvider?.RootPath))
                : ModRegistry.Instance.RootPaths.Where(Directory.Exists)
                    .SelectMany(root => Directory.EnumerateDirectories(root))
                    .Select(path => (Id: Path.GetFileName(path), Path: path));
            foreach (var mod in modDirectories)
            {
                if (string.IsNullOrEmpty(mod.Path) || !Directory.Exists(mod.Path))
                    continue;
                if (knownModIds != null
                    && !string.Equals(mod.Id, currentModId, StringComparison.OrdinalIgnoreCase)
                    && !knownModIds.Contains(mod.Id, StringComparer.OrdinalIgnoreCase))
                    continue;

                var precompiledDir = Path.Combine(mod.Path, "dll");
                if (!Directory.Exists(precompiledDir) || !searchedDirs.Add(Path.GetFullPath(precompiledDir)))
                    continue;

                foreach (var dll in Directory.GetFiles(precompiledDir, "*.dll"))
                    TryAddReference(references, dll);
            }

            // Declared references may live outside dll/ or inside an archive. Stage the same
            // selected bytes as the loader; an unrelated installation cannot supply these types.
            foreach (var mod in referenceMods ?? ModRegistry.Instance.Mods.Values)
            {
                if (mod is not ModInfo { Manifest.References.Count: > 0 })
                {
                    continue;
                }

                if (knownModIds != null && !string.Equals(mod.Id, currentModId, StringComparison.OrdinalIgnoreCase)
                    && !knownModIds.Contains(mod.Id, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var library in ModManagedReferences.Collect(mod))
                {
                    var path = PrecompiledAssemblyCache.Stage(ModAssemblyCache.CacheRoot, mod.Id,
                        library.FilePath, library.Bytes);
                    TryAddReference(references, path);
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
                if (r.Display == null || !r.Display.Replace('\\', '/').Split('/').Any(ModRootFolders.Contains))
                    continue;
                GameLogger.Log("MODS_COMPILE", $"  ref: {r.Display}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Returns the resource directory of a loose mod inside the project, or
        /// null for archives and external installations. Only these directories
        /// can supply Godot's generated res:// script paths. The relative result
        /// also distinguishes binding locations in the compilation cache.
        /// </summary>
        public static string GetScriptResourceRoot(string modRoot, string projectRoot = null)
        {
            projectRoot ??= _resRoot;
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(modRoot) || !Directory.Exists(modRoot))
                return null;
            var relative = Path.GetRelativePath(Path.GetFullPath(projectRoot), Path.GetFullPath(modRoot));
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return null;
            return relative.Replace('\\', '/');
        }

        /// <summary>
        /// Resolves source locations through the selected provider directory,
        /// including user installations outside res://. Their absolute paths
        /// belong in PDBs; Godot script-path generation is separately disabled
        /// for them. An explicit provider never falls back to another installed
        /// copy with the same id. Logical paths remain for archive sources.
        /// </summary>
        public static string ResolveSourcePath(string modId, string filePath, string modRoot = null)
        {
            var normalized = filePath.Replace('\\', '/');
            if (!string.IsNullOrEmpty(modRoot))
            {
                if (Directory.Exists(modRoot))
                {
                    var fromRoot = Path.GetFullPath(Path.Combine(modRoot, normalized));
                    if (File.Exists(fromRoot)) return fromRoot;
                }
                return filePath;
            }
            if (string.IsNullOrEmpty(_resRoot)) return filePath;
            foreach (var folder in ModRootFolders.Names)
            {
                var absolute = Path.GetFullPath(Path.Combine(_resRoot, folder, modId, normalized));
                if (File.Exists(absolute)) return absolute;
            }
            return filePath;
        }

        private static string ResolveBuildOutputDir()
        {
            if (!GameEnvironment.IsRuntime)
                return null;

            return BuildOutputDirectory;
        }

        private static void TryAddReference(List<MetadataReference> references, string dllPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(dllPath);
            if (FrameworkReferencePack.IsFrameworkAssembly(fileName) || CoreAssemblyContract.IsPinnedAssembly(fileName) || !File.Exists(dllPath))
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
            var name = assembly.GetName().Name;
            if (FrameworkReferencePack.IsFrameworkAssembly(name) || CoreAssemblyContract.IsPinnedAssembly(name))
                return;
            try
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            catch { }
        }
    }
}
