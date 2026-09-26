using System.IO;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the filesystem half of <see cref="ModUninstaller"/>. The registry
    /// lookup path needs a live mod set, so it is covered by integration; here
    /// the guarantees are: a mod directory is removed recursively, a zip mod is
    /// removed as a file, and a missing target reports failure instead of
    /// throwing. <see cref="ModRegistry.AllDiscoveredMods"/> is never seeded, so
    /// unknown ids must not be deletable.
    /// </summary>
    public class ModUninstallerTests : IDisposable
    {
        private readonly string _testDir;

        public ModUninstallerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"mod_uninstaller_test_{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        [Fact]
        public void DeleteModFiles_Folder_RemovesRecursively()
        {
            var modDir = Path.Combine(_testDir, "some_mod");
            Directory.CreateDirectory(Path.Combine(modDir, "scenes"));
            File.WriteAllText(Path.Combine(modDir, "manifest.json"), "{}");
            File.WriteAllText(Path.Combine(modDir, "scenes", "a.tscn"), "");

            var result = ModUninstaller.DeleteModFiles(modDir);

            Assert.True(result);
            Assert.False(Directory.Exists(modDir));
        }

        [Fact]
        public void DeleteModFiles_ZipFile_RemovesArchive()
        {
            var zipPath = Path.Combine(_testDir, "some_mod.zip");
            File.WriteAllBytes(zipPath, new byte[] { 0x50, 0x4B, 0x03, 0x04 });

            var result = ModUninstaller.DeleteModFiles(zipPath);

            Assert.True(result);
            Assert.False(File.Exists(zipPath));
        }

        [Fact]
        public void DeleteModFiles_MissingPath_ReturnsFalse()
        {
            var result = ModUninstaller.DeleteModFiles(Path.Combine(_testDir, "nonexistent"));

            Assert.False(result);
        }

        [Fact]
        public void DeleteModFiles_EmptyPath_ReturnsFalse()
        {
            Assert.False(ModUninstaller.DeleteModFiles(""));
            Assert.False(ModUninstaller.DeleteModFiles(null));
        }

        [Fact]
        public void CanUninstall_NullModInfo_ReturnsFalse()
        {
            Assert.False(ModUninstaller.CanUninstall((IModInfo)null));
        }

        [Fact]
        public void CanUninstall_ModOutsideKnownRoots_ReturnsFalse()
        {
            var modDir = Path.Combine(_testDir, "external_mod");
            Directory.CreateDirectory(modDir);
            var info = new ModInfo
            {
                Id = "external_mod",
                FileProvider = new FolderModFileProvider(modDir, "external_mod"),
            };

            Assert.False(ModUninstaller.CanUninstall(info));
        }
    }
}
