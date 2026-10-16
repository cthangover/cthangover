using System.Reflection;
using Cthangover.Core.Mods.Discovery;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Resets the process-wide discovery registry through reflection, so it
    /// must not overlap with test classes that boot the mod registry in
    /// parallel (they would see emptied scanner/loader lists).
    /// </summary>
    [CollectionDefinition("mod-discovery-registry", DisableParallelization = true)]
    public class ModDiscoveryRegistryCollectionDefinition
    {
    }

    [Collection("mod-discovery-registry")]
    public class ModDiscoveryRegistryTests : IDisposable
    {
        public ModDiscoveryRegistryTests()
        {
            CleanupAndReset();
        }

        public void Dispose()
        {
            CleanupAndReset();
        }

        private static void CleanupAndReset()
        {
            var initField = typeof(ModDiscoveryRegistry).GetField("_initialized",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (initField != null)
                initField.SetValue(null, false);

            var scannersField = typeof(ModDiscoveryRegistry).GetField("_scanners",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (scannersField != null)
            {
                var scanners = (List<IModScanner>)scannersField.GetValue(null);
                scanners?.Clear();
            }

            var loadersField = typeof(ModDiscoveryRegistry).GetField("_loaders",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (loadersField != null)
            {
                var loaders = (List<IModLoader>)loadersField.GetValue(null);
                loaders?.Clear();
            }
        }

        [Fact]
        public void Initialize_DiscoversScanners()
        {
            ModDiscoveryRegistry.Initialize();

            Assert.NotEmpty(ModDiscoveryRegistry.Scanners);
            Assert.All(ModDiscoveryRegistry.Scanners, s => Assert.NotNull(s.Name));
        }

        [Fact]
        public void Initialize_DiscoversLoaders()
        {
            ModDiscoveryRegistry.Initialize();

            Assert.NotEmpty(ModDiscoveryRegistry.Loaders);
            Assert.All(ModDiscoveryRegistry.Loaders, l => Assert.NotNull(l.Name));
        }

        [Fact]
        public void Initialize_IsIdempotent()
        {
            ModDiscoveryRegistry.Initialize();
            var scannerCount = ModDiscoveryRegistry.Scanners.Count;
            var loaderCount = ModDiscoveryRegistry.Loaders.Count;

            ModDiscoveryRegistry.Initialize();

            Assert.Equal(scannerCount, ModDiscoveryRegistry.Scanners.Count);
            Assert.Equal(loaderCount, ModDiscoveryRegistry.Loaders.Count);
        }

        [Fact]
        public void FindScanner_ExistingDirectory_ReturnsScanner()
        {
            ModDiscoveryRegistry.Initialize();
            var tempDir = Path.Combine(Path.GetTempPath(), $"registry_test_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var scanner = ModDiscoveryRegistry.FindScanner(tempDir);

                Assert.NotNull(scanner);
            }
            finally
            {
                Directory.Delete(tempDir);
            }
        }

        [Fact]
        public void FindScanner_NonExistentPath_ReturnsNull()
        {
            ModDiscoveryRegistry.Initialize();

            var scanner = ModDiscoveryRegistry.FindScanner("/definitely/not/a/real/path_12345");

            Assert.Null(scanner);
        }

        [Fact]
        public void FindLoader_ExistingDirectory_ReturnsFolderLoader()
        {
            ModDiscoveryRegistry.Initialize();
            var tempDir = Path.Combine(Path.GetTempPath(), $"registry_test_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                var loader = ModDiscoveryRegistry.FindLoader(tempDir);

                Assert.NotNull(loader);
                Assert.Equal("Folder", loader.Name);
            }
            finally
            {
                Directory.Delete(tempDir);
            }
        }

        [Fact]
        public void FindLoader_ZipPath_ReturnsZipLoader()
        {
            ModDiscoveryRegistry.Initialize();

            var loader = ModDiscoveryRegistry.FindLoader("some_mod.zip");

            Assert.NotNull(loader);
            Assert.Equal("Zip", loader.Name);
        }

        [Fact]
        public void FindLoader_UnrecognizedPath_ReturnsNull()
        {
            ModDiscoveryRegistry.Initialize();

            var loader = ModDiscoveryRegistry.FindLoader("/some/unknown/file.xyz");

            Assert.Null(loader);
        }
    }
}
