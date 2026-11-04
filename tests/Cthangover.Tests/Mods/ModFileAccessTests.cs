namespace Engine.Tests.Mods
{
    using System;
    using System.IO;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;
    using Engine.Tests.Mods.Mocks;

    /// <summary>Consumers can request seekability or a real file without extracting the entire package.</summary>
    public class ModFileAccessTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "mod_access_" + Guid.NewGuid().ToString("N"));
        public ModFileAccessTests() => Directory.CreateDirectory(root);
        public void Dispose() => Directory.Delete(root, true);

        [Theory]
        [InlineData(0)]
        [InlineData(1048576)]
        public void SeekableAccessWorksWithDiskAndMemoryBudgets(int budget)
        {
            using var provider = new SevenZipModFileProvider(SevenZipFixtures.PathFor("encrypted_headers_solid"), "test", SevenZipFixtures.Password);
            using var stream = ModFileAccess.OpenSeekable(provider, "Data/payload.bin", budget);
            Assert.True(stream.CanSeek);
            Assert.Equal(262144, stream.Length);
            stream.Seek(255, SeekOrigin.Begin);
            Assert.Equal(255, stream.ReadByte());
            stream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(0, stream.ReadByte());
            Assert.Null(ModFileAccess.OpenSeekable(provider, "missing.txt", budget));
        }

        [Fact]
        public void MaterializationPreservesUnchangedFilesAndCopiesOnlyRequestedContent()
        {
            using var provider = new SevenZipModFileProvider(SevenZipFixtures.PathFor("plain"), "test");
            var destination = Path.Combine(root, "cache", "payload.bin");
            Assert.True(ModFileAccess.Materialize(provider, "Data/payload.bin", destination));
            Assert.Equal(SevenZipFixtures.Payload(), File.ReadAllBytes(destination));
            var timestamp = File.GetLastWriteTimeUtc(destination);
            Assert.False(ModFileAccess.Materialize(provider, "Data/payload.bin", destination));
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(destination));
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public void MockProviderStreamsBinaryFilesWithoutTextConversion()
        {
            var provider = new MockModFileProvider("stream_contract");
            var bytes = new byte[] { 0, 255, 128, 65 };
            provider.AddBinaryFile("data.bin", bytes);
            using var stream = provider.OpenStream("data.bin");
            using var copy = new MemoryStream();
            Assert.NotNull(stream);
            stream.CopyTo(copy);
            Assert.Equal(bytes, copy.ToArray());
            Assert.Null(provider.OpenStream("missing.bin"));
        }
    }
}
