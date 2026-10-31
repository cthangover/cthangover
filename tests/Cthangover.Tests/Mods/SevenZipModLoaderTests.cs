using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Discovery;
using Cthangover.Core.Mods.Pipeline;

namespace Engine.Tests.Mods
{
    public class SevenZipModLoaderTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "sevenzip_loader_" + Guid.NewGuid().ToString("N"));
        public SevenZipModLoaderTests() => Directory.CreateDirectory(root);
        public void Dispose() => Directory.Delete(root, true);

        [Theory]
        [InlineData("mod.7z", true)]
        [InlineData("MOD.7Z", true)]
        [InlineData("mod.zip", false)]
        [InlineData("mod.7z.tmp", false)]
        [InlineData(null, false)]
        public void RecognizesOnly7zFiles(string path, bool expected) => Assert.Equal(expected, new SevenZipModLoader().IsCanProvide(path));

        [Fact]
        public void DirectoryNamed7zIsNotAnArchive()
        {
            var path = Path.Combine(root, "folder.7z");
            Directory.CreateDirectory(path);
            Assert.False(new SevenZipModLoader().IsCanProvide(path));
        }

        [Theory]
        [InlineData("plain", null)]
        [InlineData("encrypted_headers_solid", SevenZipFixtures.Password)]
        public void LoaderReadsManifestUsingRememberedPassword(string name, string password)
        {
            var path = SevenZipFixtures.PathFor(name);
            var store = new ArchivePasswordStore(Path.Combine(root, "passwords.json"));
            store.Set(path, password);
            var loader = new SevenZipModLoader(store);
            using var provider = loader.CreateProvider(path, "test_sevenzip");
            Assert.Equal("test_sevenzip", ModDiscoveryService.ReadManifest(provider, out _).Id);
            Assert.Equal("7z", loader.Name);
            Assert.Equal("my.mod.v1", loader.GetFallbackModId(path, "my.mod.v1.7z"));
        }
    }
}
