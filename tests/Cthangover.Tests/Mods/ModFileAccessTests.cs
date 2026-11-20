namespace Engine.Tests.Mods
{
    using System;
    using System.IO;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;

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
        public void ZipProviderStreamsBinaryFilesWithoutTextConversion()
        {
            // A real provider must stream binary payload bytes through
            // OpenStream unchanged (no text decoding, no encoding round trip):
            // the byte set deliberately contains values that differ between a
            // raw byte stream and any text-mangled variant.
            var bytes = new byte[] { 0, 255, 128, 65, 10, 13 };
            var zipPath = Path.Combine(root, "binary_stream_mod.zip");
            using (var archive = new System.IO.Compression.ZipArchive(
                System.IO.File.Open(zipPath, System.IO.FileMode.Create),
                System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("data.bin");
                using var entryStream = entry.Open();
                entryStream.Write(bytes, 0, bytes.Length);
            }

            using var provider = new ZipModFileProvider(zipPath, "binary_stream_mod");
            using var stream = provider.OpenStream("data.bin");
            Assert.NotNull(stream);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            Assert.Equal(bytes, copy.ToArray());
            Assert.Null(provider.OpenStream("missing.bin"));
        }
    }
}
