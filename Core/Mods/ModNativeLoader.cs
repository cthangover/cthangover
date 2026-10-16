using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Loads native shared libraries from mod <c>depends_native</c>
    /// manifest declarations and wires them into the P/Invoke resolution chain
    /// via <c>NativeLibrary.SetDllImportResolver</c>.
    ///
    /// <b>Discovery</b> — iterates every mod whose manifest declares
    /// <c>depends_native</c> glob patterns, collects platform-appropriate
    /// files (.dll / .so / .dylib), and copies them to a stable cache
    /// directory so that even zip-based mods can serve native code.
    ///
    /// <b>Resolution</b> — a single <c>DllImportResolver</c> is installed
    /// on the core project assembly. When the runtime searches for a
    /// native library named in a <c>[DllImport]</c> attribute, the
    /// resolver checks the accumulated cache and returns the handle.
    /// Falls through to default resolution for libraries not found in
    /// the cache.
    ///
    /// <b>Placement</b> — called from <c>ModRegistry.Initialize()</c>
    /// before any managed code loads, so that P/Invoke calls in static
    /// constructors or type initializers never see a missing library.
    ///
    /// <b>Lifecycle</b> — loaded handles are tracked for cleanup during
    /// <c>ModRegistry.Reload()</c>. Each native library is loaded once;
    /// if multiple mods provide the same library name, the first one
    /// wins and duplicates are logged as warnings.
    /// </summary>
    public static class ModNativeLoader
    {
        /// <summary>
        /// Absolute path to the native library cache directory
        /// (<c>user://mod_cache/native/</c>). Resolved once at static
        /// init with an OS temp fallback for non-runtime contexts.
        /// </summary>
        public static readonly string CacheRoot;

        private static readonly Dictionary<string, string> _nameToPath = new();
        private static readonly Dictionary<string, IntPtr> _loadedHandles = new();
        private static bool _resolverInstalled;

        static ModNativeLoader()
        {
            if (GameEnvironment.IsRuntime)
            {
                try
                {
                    CacheRoot = ProjectSettings.GlobalizePath("user://mod_cache/native/");
                }
                catch
                {
                    CacheRoot = Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                        "cthangover", "mod_cache", "native");
                }
            }
            else
            {
                CacheRoot = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "cthangover", "mod_cache", "native");
            }
        }

        /// <summary>
        /// Scans each loaded mod's <c>depends_native</c> patterns for
        /// platform-native shared libraries, copies them to the cache,
        /// and registers a <c>DllImportResolver</c> on the core assembly.
        /// Must be called before any managed code that triggers P/Invoke
        /// resolution — typically from <c>ModRegistry.Initialize()</c>
        /// right before precompiled DLL loading.
        /// </summary>
        public static void LoadNativeDlls(IEnumerable<IModInfo> mods, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(CacheRoot))
                Directory.CreateDirectory(CacheRoot);

            foreach (var mod in mods)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var modInfo = mod as ModInfo;
                var nativePatterns = modInfo?.Manifest?.DependsNative;
                if (nativePatterns == null || nativePatterns.Count == 0)
                    continue;

                var nativeFiles = CollectNativeFiles(mod.FileProvider, nativePatterns);
                if (nativeFiles.Count == 0)
                    continue;

                GameLogger.Log("MODS_NATIVE", $"ModNativeLoader.LoadNativeDlls: found {nativeFiles.Count} native library(s) in mod '{mod.Id}'");

                foreach (var (filePath, bytes) in nativeFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var libraryName = NormalizeLibraryName(fileName);
                        var cachedPath = Path.Combine(CacheRoot, $"{mod.Id}_native_{fileName}");

                        File.WriteAllBytes(cachedPath, bytes);

                        if (_nameToPath.ContainsKey(libraryName))
                        {
                            GameLogger.Log("MODS_NATIVE",
                                $"ModNativeLoader.LoadNativeDlls: native library '{libraryName}' already registered " +
                                $"(from a previous mod), skipping duplicate from mod '{mod.Id}'",
                                LogLevel.Warning);
                            continue;
                        }

                        _nameToPath[libraryName] = cachedPath;
                        GameLogger.Log("MODS_NATIVE",
                            $"ModNativeLoader.LoadNativeDlls: registered native library '{libraryName}' " +
                            $"from mod '{mod.Id}' -> '{cachedPath}'");
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("MODS_NATIVE",
                            $"ModNativeLoader.LoadNativeDlls: failed to cache native library '{filePath}' " +
                            $"from mod '{mod.Id}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            if (_nameToPath.Count > 0)
                EnsureResolverInstalled();
        }

        /// <summary>
        /// Releases all native library handles loaded through the
        /// resolver. Called during <c>ModRegistry.Reload()</c> to
        /// allow DLL files to be overwritten on the next load cycle.
        /// </summary>
        public static void FreeAll()
        {
            foreach (var kvp in _loadedHandles)
            {
                try { NativeLibrary.Free(kvp.Value); }
                catch { }
            }
            _loadedHandles.Clear();
            _nameToPath.Clear();
        }

        private static void EnsureResolverInstalled()
        {
            if (_resolverInstalled)
                return;

            _resolverInstalled = true;

            System.Runtime.Loader.AssemblyLoadContext.Default.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;

            GameLogger.Log("MODS_NATIVE",
                "ModNativeLoader: global native DLL resolver installed");
        }

        /// <summary>
        /// Installs the DllImportResolver on a specific assembly.
        /// Call after loading a precompiled mod assembly that may contain [DllImport] attributes.
        /// </summary>
        public static void InstallResolverOnAssembly(Assembly assembly)
        {
            if (_nameToPath.Count == 0) return;
            NativeLibrary.SetDllImportResolver(assembly, OnResolvingUnmanagedDllAssembly);
        }

        private static IntPtr OnResolvingUnmanagedDllAssembly(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            libraryName = NormalizeLibraryName(libraryName);
            if (_loadedHandles.TryGetValue(libraryName, out var cachedHandle))
                return cachedHandle;

            if (!_nameToPath.TryGetValue(libraryName, out var cachedPath))
                return IntPtr.Zero;

            if (NativeLibrary.TryLoad(cachedPath, out var handle))
            {
                _loadedHandles[libraryName] = handle;
                GameLogger.Log("MODS_NATIVE",
                    $"ModNativeLoader: loaded '{libraryName}' from '{cachedPath}'");
                return handle;
            }

            return IntPtr.Zero;
        }

        private static IntPtr OnResolvingUnmanagedDll(Assembly assembly, string libraryName)
        {
            libraryName = NormalizeLibraryName(libraryName);
            if (_loadedHandles.TryGetValue(libraryName, out var cachedHandle))
                return cachedHandle;

            if (!_nameToPath.TryGetValue(libraryName, out var cachedPath))
                return IntPtr.Zero;

            if (NativeLibrary.TryLoad(cachedPath, out var handle))
            {
                _loadedHandles[libraryName] = handle;
                GameLogger.Log("MODS_NATIVE",
                    $"ModNativeLoader: loaded '{libraryName}' from '{cachedPath}'");
                return handle;
            }

            GameLogger.Log("MODS_NATIVE",
                $"ModNativeLoader: failed to load '{libraryName}' from '{cachedPath}'",
                LogLevel.Error);
            return IntPtr.Zero;
        }

        /// <summary>
        /// Platform-neutral key for a native library: no extension and no
        /// Unix <c>lib</c> prefix. <c>[DllImport("Live2DCubismCore")]</c> must
        /// find <c>Live2DCubismCore.dll</c> on Windows as well as
        /// <c>libLive2DCubismCore.so</c> / <c>.dylib</c> on Linux and macOS.
        /// </summary>
        public static string NormalizeLibraryName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            var fileName = Path.GetFileName(name);
            foreach (var ext in new[] { ".dll", ".so", ".dylib" })
            {
                if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    fileName = fileName.Substring(0, fileName.Length - ext.Length);
                    break;
                }
            }
            if (fileName.StartsWith("lib", StringComparison.Ordinal) && fileName.Length > 3)
                fileName = fileName.Substring(3);
            return fileName;
        }

        private static List<(string FilePath, byte[] Bytes)> CollectNativeFiles(
            IModFileProvider provider, List<string> patterns)
        {
            var result = new List<(string, byte[])>();
            foreach (var pattern in patterns)
            {
                var prefix = pattern;
                var suffix = "*";
                var sepIndex = pattern.LastIndexOf('/');
                if (sepIndex >= 0)
                {
                    prefix = pattern.Substring(0, sepIndex);
                    suffix = pattern.Substring(sepIndex + 1);
                }

                CollectNativeFilesRecursive(provider, prefix, file =>
                {
                    if (!IsPlatformFile(file))
                        return false;

                    if (suffix == "*")
                        return true;

                    var fileName = file.Replace('\\', '/');
                    var fileNamePart = fileName.Substring(fileName.LastIndexOf('/') + 1);

                    return suffix == "*" ||
                           (suffix.StartsWith("*.") &&
                            fileNamePart.EndsWith(suffix.Substring(1), StringComparison.OrdinalIgnoreCase));
                }, result);
            }

            return result;
        }

        private static void CollectNativeFilesRecursive(
            IModFileProvider provider,
            string directory,
            Func<string, bool> fileFilter,
            List<(string, byte[])> result)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectNativeFilesRecursive(provider, subDir, fileFilter, result);
                }
                else if (fileFilter(entry))
                {
                    var bytes = provider.ReadFileBinary(entry);
                    if (bytes != null)
                        result.Add((entry, bytes));
                }
            }
        }

        private static bool IsPlatformFile(string fileName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return fileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase);

            return false;
        }
    }
}
