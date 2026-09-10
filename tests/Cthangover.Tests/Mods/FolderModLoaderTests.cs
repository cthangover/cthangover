using System.IO;
using Cthangover.Core.Mods.Discovery;
using Cthangover.Core.Mods.Providers;

namespace Engine.Tests.Mods
{
    public class FolderModLoaderTests : IDisposable
    {
        private readonly string _testDir;

        public FolderModLoaderTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"folder_loader_test_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        [Fact]
        public void IsCanProvide_ExistingDirectory_ReturnsTrue()
        {
            var loader = new FolderModLoader();

            var result = loader.IsCanProvide(_testDir);

            Assert.True(result);
        }

        [Fact]
        public void IsCanProvide_NonExistentPath_ReturnsFalse()
        {
            var loader = new FolderModLoader();

            var result = loader.IsCanProvide(Path.Combine(_testDir, "nonexistent"));

            Assert.False(result);
        }

        [Fact]
        public void IsCanProvide_FilePath_ReturnsFalse()
        {
            var filePath = Path.Combine(_testDir, "file.txt");
            File.WriteAllText(filePath, "content");
            var loader = new FolderModLoader();

            var result = loader.IsCanProvide(filePath);

            Assert.False(result);
        }

        [Fact]
        public void CreateProvider_ReturnsFolderModFileProvider()
        {
            var loader = new FolderModLoader();

            var provider = loader.CreateProvider(_testDir, "test_mod");

            Assert.IsType<FolderModFileProvider>(provider);
            Assert.Equal("test_mod", provider.Mod);
        }

        [Fact]
        public void GetFallbackModId_ReturnsEntryName()
        {
            var loader = new FolderModLoader();

            var fallbackId = loader.GetFallbackModId("/path/to/mods/my_cool_mod", "my_cool_mod");

            Assert.Equal("my_cool_mod", fallbackId);
        }
    }
}
