using System.IO;
using System.IO.Compression;
using System.Text;
using Cthangover.Core.Mods.Discovery;
using Cthangover.Core.Mods.Providers;

namespace Engine.Tests.Mods
{
    public class ZipModLoaderTests : IDisposable
    {
        private readonly string _testDir;

        public ZipModLoaderTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"zip_loader_test_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        [Fact]
        public void IsCanProvide_ZipPath_ReturnsTrue()
        {
            var loader = new ZipModLoader();

            var result = loader.IsCanProvide("mod.zip");

            Assert.True(result);
        }

        [Fact]
        public void IsCanProvide_UpperCaseZip_ReturnsTrue()
        {
            var loader = new ZipModLoader();

            var result = loader.IsCanProvide("MOD.ZIP");

            Assert.True(result);
        }

        [Fact]
        public void IsCanProvide_NonZipPath_ReturnsFalse()
        {
            var loader = new ZipModLoader();

            var result = loader.IsCanProvide("mod.txt");

            Assert.False(result);
        }

        [Fact]
        public void IsCanProvide_DirectoryLikeZip_ReturnsFalse()
        {
            var dirPath = Path.Combine(_testDir, "mod.zip");
            Directory.CreateDirectory(dirPath);
            var loader = new ZipModLoader();

            var result = loader.IsCanProvide(dirPath);

            Assert.True(result);
        }

        [Fact]
        public void CreateProvider_ReturnsZipModFileProvider_AndCanReadManifest()
        {
            var zipPath = Path.Combine(_testDir, "test_mod.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("manifest.json");
                using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
                {
                    writer.Write(@"{""id"": ""test_zip_mod"", ""name"": ""Test Zip Mod""}");
                }
            }

            var loader = new ZipModLoader();
            var provider = loader.CreateProvider(zipPath, "test_zip_mod");

            Assert.IsType<ZipModFileProvider>(provider);
            Assert.Equal("test_zip_mod", provider.Mod);
            Assert.True(provider.FileExists("manifest.json"));

            provider.Dispose();
        }

        [Fact]
        public void GetFallbackModId_StripsZipExtension()
        {
            var loader = new ZipModLoader();

            var fallbackId = loader.GetFallbackModId("/path/to/mods/mod.zip", "mod.zip");

            Assert.Equal("mod", fallbackId);
        }

        [Fact]
        public void GetFallbackModId_DottedName_StripsOnlyZipExtension()
        {
            var loader = new ZipModLoader();

            var fallbackId = loader.GetFallbackModId("/path/to/mods/my.mod.v1.2.zip", "my.mod.v1.2.zip");

            Assert.Equal("my.mod.v1.2", fallbackId);
        }
    }
}
