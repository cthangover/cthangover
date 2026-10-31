namespace Engine.Tests.Mods
{
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Pipeline;
    using Engine.Tests.Mods.Mocks;

    /// <summary>
    /// Reading a mod's manifest during discovery: a missing file and a file that is there but wrong are
    /// different problems, and the author is told which (an unknown category used to read "no manifest found").
    /// </summary>
    [Collection("mod-discovery-registry")]
    public class ModDiscoveryServiceTests : System.IDisposable
    {
        private readonly string archiveRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "locked_discovery_" + System.Guid.NewGuid().ToString("N"));
        private readonly System.Collections.Generic.Dictionary<string, IModInfo> discovered = new();

        public void Dispose()
        {
            foreach (var mod in discovered.Values)
                mod.FileProvider.Dispose();
            var path = System.IO.Path.Combine(archiveRoot, "locked.7z");
            ArchivePasswordStore.Default.SetSession(path, null);
            ArchiveUnlockService.Default.Remove(path);
            if (System.IO.Directory.Exists(archiveRoot))
                System.IO.Directory.Delete(archiveRoot, true);
        }

        [Fact]
        public void EncryptedPackageRemainsVisibleAndIsRediscoveredAfterSessionUnlock()
        {
            System.IO.Directory.CreateDirectory(archiveRoot);
            var path = System.IO.Path.Combine(archiveRoot, "locked.7z");
            System.IO.File.Copy(SevenZipFixtures.PathFor("encrypted_headers_solid"), path);
            Cthangover.Core.Mods.Discovery.ModDiscoveryRegistry.Initialize();
            var scanner = new Cthangover.Core.Mods.Discovery.GameDirModScanner();
            var priorities = new System.Collections.Generic.Dictionary<string, int>();
            ModDiscoveryService.DiscoverMods(discovered, priorities, scanner, archiveRoot, 0, default);
            Assert.Empty(discovered);
            Assert.True(HasLockedPath(path));
            ArchiveUnlockService.Default.Unlock(path, SevenZipFixtures.Password);
            ModDiscoveryService.DiscoverMods(discovered, priorities, scanner, archiveRoot, 0, default);
            Assert.Equal("Привет из 7z!\n", discovered["test_sevenzip"].FileProvider.ReadFileText("Data/привет.txt"));
            Assert.False(HasLockedPath(path));
        }

        private static bool HasLockedPath(string path)
        {
            foreach (var item in ArchiveUnlockService.Default.GetUnavailable())
                if (item.Path == System.IO.Path.GetFullPath(path) && item.NeedsPassword)
                    return true;
            return false;
        }

        [Fact]
        public void ReadManifest_WrongCategory_SaysTheManifestIsInvalidAndWhy()
        {
            var provider = new MockModFileProvider("broken");
            provider.AddFile("manifest.json", """{ "id": "broken", "category": "gameplayy" }""");

            Assert.Null(ModDiscoveryService.ReadManifest(provider, out var problem));
            Assert.StartsWith("invalid manifest.json: ", problem);
            Assert.Contains("category", problem);
        }

        [Fact]
        public void ReadManifest_BrokenJsonOrEmptyFile_IsInvalid()
        {
            var broken = new MockModFileProvider("broken");
            broken.AddFile("manifest.json", """{ "id": "broken", """);
            Assert.Null(ModDiscoveryService.ReadManifest(broken, out var problem));
            Assert.StartsWith("invalid manifest.json: ", problem);

            var empty = new MockModFileProvider("empty");
            empty.AddFile("manifest.json", "  ");
            Assert.Null(ModDiscoveryService.ReadManifest(empty, out problem));
            Assert.Equal("invalid manifest.json: the file is empty", problem);
        }

        [Fact]
        public void ReadManifest_NoFile_SaysSo()
        {
            Assert.Null(ModDiscoveryService.ReadManifest(new MockModFileProvider("none"), out var problem));
            Assert.Equal("no manifest found", problem);
        }

        [Fact]
        public void ReadManifest_Valid_ReturnsItWithoutAProblem()
        {
            var provider = new MockModFileProvider("fine");
            provider.AddFile("manifest.json", """{ "id": "fine", "category": "library, gameplay" }""");

            var manifest = ModDiscoveryService.ReadManifest(provider, out var problem);

            Assert.Equal("fine", manifest.Id);
            Assert.Null(problem);
        }
    }
}
