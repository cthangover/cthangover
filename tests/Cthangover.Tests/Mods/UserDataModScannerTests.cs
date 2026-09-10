using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods.Discovery;
using Cthangover.Core.Utils;

namespace Engine.Tests.Mods
{
    public class UserDataModScannerTests : IDisposable
    {
        private readonly string _testDir;

        public UserDataModScannerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"userdata_scanner_test_{System.Guid.NewGuid():N}");
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
            var scanner = new UserDataModScanner();

            var result = scanner.IsCanProvide(_testDir);

            Assert.True(result);
        }

        [Fact]
        public void IsCanProvide_NonExistentPath_ReturnsFalse()
        {
            var scanner = new UserDataModScanner();

            var result = scanner.IsCanProvide(Path.Combine(_testDir, "nonexistent"));

            Assert.False(result);
        }

        [Fact]
        public void GetRoots_WhenNotRuntime_ReturnsRawUserModsPath()
        {
            var scanner = new UserDataModScanner();

            var roots = new List<ModSourceRoot>(scanner.GetRoots());

            Assert.Single(roots);
            Assert.Equal("user://mods/", roots[0].Url);
            Assert.Equal(0, roots[0].Priority);
        }

        [Fact]
        public void GetRoots_WhenNotRuntime_ReturnedRootIsNotAccessible()
        {
            var scanner = new UserDataModScanner();
            var roots = new List<ModSourceRoot>(scanner.GetRoots());

            var isAccessible = scanner.IsCanProvide(roots[0].Url);

            Assert.False(isAccessible);
        }

        [Fact]
        public void Discover_EnumeratesFilesAndFolders()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "subfolder"));
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "content");
            File.WriteAllText(Path.Combine(_testDir, "mod.zip"), "");
            var scanner = new UserDataModScanner();

            var entries = new List<DiscoveredModEntry>(scanner.Discover(_testDir));

            Assert.Equal(3, entries.Count);
            Assert.Contains(entries, e => e.EntryName == "subfolder");
            Assert.Contains(entries, e => e.EntryName == "file.txt");
            Assert.Contains(entries, e => e.EntryName == "mod.zip");
            Assert.All(entries, e => Assert.False(e.Url.Contains('\\')));
        }

        [Fact]
        public void Discover_EmptyDirectory_ReturnsEmpty()
        {
            var scanner = new UserDataModScanner();

            var entries = scanner.Discover(_testDir);

            Assert.Empty(entries);
        }
    }
}
