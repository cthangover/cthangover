using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cthangover.Live2D.Cubism.Framework;

namespace Engine.Tests.Live2D
{
    /// <summary>
    /// Test environment for the Live2D unit tests. Locates the repository root on
    /// disk (the unit runner may start with an arbitrary working directory) and
    /// makes the native CubismCore library resolvable for P/Invoke calls: at game
    /// runtime Live2DCubismCore.dll is placed next to the mod binaries, while in
    /// the unit test process a DllImport resolver (registered from this module
    /// initializer for both the Live2D assembly and the test assembly) loads it
    /// directly from mods/live2d/native. Without the resolver, tests that exercise
    /// the real native core (Haru.moc3 fixture) would fail with DllNotFoundException.
    /// </summary>
    internal static class Live2DTestRuntime
    {
        private static string _repoRoot;

        /// <summary>Absolute path of the repository root (the directory holding AGENTS.md).</summary>
        public static string RepoRoot => _repoRoot ??= FindRepoRoot();

        /// <summary>Root of the Cubism SDK sample resources used as test fixtures.</summary>
        public static string SdkResources =>
            Path.Combine(RepoRoot, "sdk", "CubismSdkForNative-5-r.5", "Samples", "Resources");

        /// <summary>Absolute path of the native CubismCore library shipped with the mod.</summary>
        public static string NativeCorePath =>
            Path.Combine(RepoRoot, "mods", "live2d", "native", "Live2DCubismCore.dll");

        [ModuleInitializer]
        internal static void Init()
        {
            // Registers the resolver for BOTH the Live2D assembly (mod P/Invokes)
            // and the test assembly (CubismCoreTestAccess P/Invokes).
            RegisterResolver(typeof(CubismNativeModel).Assembly);
            RegisterResolver(typeof(Live2DTestRuntime).Assembly);
        }

        private static void RegisterResolver(System.Reflection.Assembly assembly)
        {
            try
            {
                NativeLibrary.SetDllImportResolver(assembly, (libraryName, assemblyPath, searchPath) =>
                {
                    if (string.Equals(libraryName, "Live2DCubismCore", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(NativeCorePath))
                    {
                        return NativeLibrary.Load(NativeCorePath);
                    }

                    return IntPtr.Zero; // fall back to default probing
                });
            }
            catch (InvalidOperationException)
            {
                // A resolver is already registered for this assembly — nothing to do.
            }
        }

        /// <summary>
        /// Loads the Haru sample model through the real mod pipeline (native CubismCore).
        /// The caller owns the returned instance; it is IDisposable.
        /// </summary>
        public static CubismNativeModel LoadHaruModel()
        {
            var mocBytes = File.ReadAllBytes(Path.Combine(SdkResources, "Haru", "Haru.moc3"));
            var model = new CubismNativeModel();
            model.LoadMoc(mocBytes);
            return model;
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "Repository root (AGENTS.md) not found walking up from " + AppContext.BaseDirectory);
        }
    }
}
