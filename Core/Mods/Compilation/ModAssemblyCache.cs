using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Compilation
{
    /// <summary>
    /// Owner of the compiled-assembly cache at
    /// <c>user://mod_cache/assemblies/</c>: path resolution, cache-key
    /// construction, hit/stale handling, artifact storage and invalidation.
    ///
    /// Cache entries are named <c>&lt;modId&gt;_&lt;hash&gt;.dll</c> plus a
    /// paired portable PDB. The hash mixes the mod's own sources with its
    /// dependency signature, the kernel assembly MVID and a codegen marker,
    /// so any of those changing forces a recompile instead of a stale load.
    /// Every DLL is stored together with its PDB and loaded from disk
    /// (file-backed) so a debugger binds line-level breakpoints in mod code.
    /// </summary>
    public static class ModAssemblyCache
    {
        /// <summary>
        /// Absolute path to the assembly cache directory
        /// (<c>user://mod_cache/assemblies/</c>). Resolved once at
        /// static init time with a fallback to the OS temp folder.
        /// </summary>
        public static readonly string CacheRoot;

        static ModAssemblyCache()
        {
            var testCache = System.Environment.GetEnvironmentVariable("CTHANGOVER_TEST_CACHE");
            if (!string.IsNullOrEmpty(testCache))
            {
                CacheRoot = System.IO.Path.GetFullPath(testCache);
                return;
            }
            if (GameEnvironment.IsRuntime)
            {
                try
                {
                    CacheRoot = ProjectSettings.GlobalizePath("user://mod_cache/assemblies/");
                }
                catch
                {
                    CacheRoot = System.IO.Path.Combine(
                        GameEnvironment.LocalApplicationData,
                        "cthangover", "mod_cache", "assemblies");
                }
            }
            else
            {
                CacheRoot = System.IO.Path.Combine(
                    GameEnvironment.LocalApplicationData,
                    "cthangover", "mod_cache", "assemblies");
            }
        }

        /// <summary>
        /// The artifact (<c>&lt;id&gt;_&lt;hash&gt;.dll</c>) each mod compiled to in
        /// this process, recorded by <see cref="ModCompiler.EnsureCompiled"/>. It
        /// is the source of truth for dependents: their cache key includes it
        /// (<see cref="BuildDependencySignature"/>) and their compilation references
        /// exactly it (<see cref="ModReferenceProvider"/>) instead of guessing the
        /// newest file by timestamp — after a hot reload back to earlier sources
        /// the right artifact is the older file.
        /// </summary>
        public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> CurrentArtifacts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Builds a deterministic signature of a mod's dependencies for the
        /// compilation cache key: every dependency id together with the version that
        /// was actually loaded (or <c>missing</c>). Renaming or upgrading a dependency
        /// therefore forces dependents to recompile against the new assembly instead
        /// of loading a stale cached DLL that links to the old assembly name.
        /// </summary>
        public static string BuildDependencySignature(ModInfo modInfo, IDictionary<string, IModInfo> mods)
        {
            var dependencies = modInfo?.Manifest?.DependencyIds;
            if (dependencies == null || dependencies.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            foreach (var depId in dependencies.Distinct().OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                var version = mods != null && mods.TryGetValue(depId, out var dependency) ? dependency.Version : null;
                // The dependency's own compiled artifact (content hash) joins the
                // key: a dependency whose sources changed is a new assembly, and its
                // dependents must recompile against it — otherwise a hot reload of
                // the dependency keeps dependents linked to members that no longer
                // exist (MissingMethodException at runtime instead of a compile error).
                var artifact = CurrentArtifacts.TryGetValue(depId, out var path) ? "#" + Path.GetFileNameWithoutExtension(path) : string.Empty;
                parts.Add($"{depId}@{version ?? "missing"}{artifact}");
            }

            return string.Join(";", parts);
        }

        /// <summary>
        /// Computes the cache path for one compilation:
        /// <c>&lt;CacheRoot&gt;/&lt;modId&gt;_&lt;hash&gt;.dll</c>. The hash
        /// covers the source list plus three extra factors: the dependency
        /// signature, the kernel assembly identity and the codegen marker.
        /// </summary>
        /// <param name="generatorsAvailable">Whether Godot source generators
        /// were resolved for this compilation — decides the codegen marker.</param>
        public static string BuildCachePath(string modId, List<(string FilePath, string Source)> sourceList, string dependencySignature, bool generatorsAvailable)
        {
            // Generated Godot script bindings change the assembly layout; the
            // marker keeps cached DLLs without them from being reused. The marker
            // also records whether generators were available: a cache entry built
            // without them must never be reused once they become available.
            var codegenMarker = generatorsAvailable
                ? "godot-scriptpath"
                : "legacy-no-generators";
            // Mix the core assembly identity into the cache key: when the kernel
            // itself changes (e.g. a subsystem type moved out of Core), mods compiled
            // against the previous core keep stale IL references to removed types and
            // would fail at runtime with TypeLoadException. A new core MVID forces
            // every mod to recompile against the updated kernel.
            var coreSignature = typeof(ModRegistry).Assembly.ManifestModule.ModuleVersionId.ToString("N");
            // "pdb=named": the DLL's debug directory names its cache-file PDB
            // (<id>_<hash>.pdb); entries naming "<id>.pdb" never bind symbols.
            // "refs=depends": mods compile against their dependency closure only
            // (ModCompiler.DependencyClosure); entries built against every mod
            // could hide an undeclared dependency and must not be reused.
            var generationPath = Path.Combine(CacheRoot, modId + ".generation");
            var generation = ReadGeneration(generationPath);
            var hash = ModHasher.ComputeHash(sourceList, (dependencySignature ?? string.Empty) + ";core=" + coreSignature + ";codegen=" + codegenMarker + ";pdb=named;refs=depends;generation=" + generation,
                System.Environment.GetEnvironmentVariable("CTHANGOVER_TEST_PROJECT"));
            return Path.Combine(CacheRoot, $"{modId}_{hash}.dll");
        }

        private static string ReadGeneration(string path)
        {
            if (!File.Exists(path)) return string.Empty;
            // A concurrent invalidation atomically renames the token. Permit that
            // rename while reading on Windows as well as Unix; either complete token is valid.
            using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Applies the cache policy to a computed cache path. On a valid hit
        /// (DLL plus PDB present and <c>UseAssemblyCache</c> enabled) the
        /// cached assembly is loaded and returned; otherwise null tells the caller to
        /// compile. Only incomplete pairs are removed under a lease; complete pairs
        /// stay immutable even when the cache policy forces compilation.
        /// <paramref name="modId"/> routes the load into the mod's collectible
        /// context (null callers — test engine — keep the default context).
        /// </summary>
        public static Assembly TryLoadCached(string cachedPath, string modId = null)
        {
            if (!IsUsableHit(cachedPath))
                return null;

            GameLogger.Log("MODS_COMPILE", $"ModCompiler.Compile: loading cached {cachedPath}");
            // Disk load (not byte load) keeps the module file-backed, so the
            // runtime finds the portable PDB written next to the DLL and a
            // debugger binds line-level breakpoints in mod code.
            return ModAssemblyLoader.LoadFromPath(cachedPath, modId)
                ?? ModAssemblyLoader.LoadFromBytes(File.ReadAllBytes(cachedPath));
        }

        /// <summary>
        /// The cache policy without the load: true when <paramref name="cachedPath"/>
        /// may be used as is (DLL plus PDB present and <c>UseAssemblyCache</c>
        /// enabled, or the artifact is the one a live mod context runs right now —
        /// rewriting a loaded, memory-mapped DLL fails on Windows and would only
        /// reproduce the same bytes). Otherwise false tells the caller to compile.
        /// Interrupted incomplete pairs are recovered under the publication lease.
        /// </summary>
        public static bool IsUsableHit(string cachedPath)
        {
            using var entry = AssemblyCacheEntry.Acquire(cachedPath);
            return IsUsableHit(entry, cachedPath);
        }

        /// <summary>Checks policy under the caller's existing lease, so compilation can retain it through publication.</summary>
        internal static bool IsUsableHit(AssemblyCacheEntry entry, string cachedPath)
        {
            if (!entry.HasArtifacts) { entry.RemoveIncomplete(); return false; }
            if (!ModConfig.Instance.UseAssemblyCache && !ModAssemblyLoader.IsArtifactLoaded(cachedPath)) return false;
            entry.Touch();
            return true;
        }

        /// <summary>
        /// Writes a freshly emitted compilation into the cache: creates the
        /// cache directory if needed, then stores the DLL and its portable
        /// PDB as a name-paired pair — a later cache hit requires both files.
        /// </summary>
        public static void Store(string cachedPath, MemoryStream assemblyStream, MemoryStream pdbStream)
        {
            using var entry = AssemblyCacheEntry.Acquire(cachedPath);
            entry.Publish(assemblyStream, pdbStream);
        }

        /// <summary>
        /// Invalidates a mod by advancing its cache generation. Existing pairs remain
        /// immutable for processes already loading them; age-based runner pruning reclaims them later.
        /// Returns true when cached artifacts existed and a new generation was published.
        /// </summary>
        public static bool InvalidateCache(string modId)
        {
            if (!Directory.Exists(CacheRoot))
                return false;
            var generation = Path.Combine(CacheRoot, modId + ".generation");
            using var lease = AssemblyCacheEntry.Acquire(generation);
            if (!Directory.EnumerateFiles(CacheRoot, modId + "_*.dll").Any()) return false;
            var temporary = generation + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Guid.NewGuid().ToString("N"));
                File.Move(temporary, generation, true);
            }
            finally { File.Delete(temporary); }
            return true;
        }

        /// <summary>
        /// True when <paramref name="fileName"/> is a cached assembly of
        /// <paramref name="modId"/> itself, i.e. named <c>&lt;modId&gt;_&lt;hash&gt;.dll</c>
        /// where <c>&lt;hash&gt;</c> is the hex compilation-cache key (see <see cref="ModHasher.ComputeHash"/>).
        /// </summary>
        public static bool IsSelfCacheDll(string fileName, string modId)
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
        public static bool TryParseCacheDllName(string fileName, out string modId, out string hash)
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
    }
}
