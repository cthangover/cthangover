namespace Engine.Tests.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;

    /// <summary>Shared archive contracts: caller-owned buffers, bulk streaming and existing hash-format compatibility.</summary>
    public class ArchiveModFileProviderTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "archive_contract_" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, byte[]> visited = new(StringComparer.Ordinal);
        public ArchiveModFileProviderTests() => Directory.CreateDirectory(root);
        public void Dispose() => Directory.Delete(root, true);

        [Fact]
        public void BufferedReadsDoNotExposeMutableSharedCache()
        {
            using var provider = new SevenZipModFileProvider(SevenZipFixtures.PathFor("plain"), "test");
            var first = provider.ReadFileBinary("Data/payload.bin");
            first[0] = 123;
            Assert.Equal(0, provider.ReadFileBinary("Data/payload.bin")[0]);
            using var stream = provider.OpenStream("Data/payload.bin");
            Assert.Equal(0, stream.ReadByte());
        }

        [Fact]
        public void BulkVisitorReadsSolidPackageExactlyOncePerFile()
        {
            using var provider = new SevenZipModFileProvider(SevenZipFixtures.PathFor("encrypted_headers_solid"), "test", SevenZipFixtures.Password);
            Assert.True(provider.IsEncrypted);
            provider.ValidatePassword();
            provider.VisitFiles(Visit);
            Assert.Equal(4, visited.Count);
            Assert.Equal(SevenZipFixtures.Payload(), visited["Data/payload.bin"]);
            Assert.Equal("Привет из 7z!\n", Encoding.UTF8.GetString(visited["Data/привет.txt"]));
            Assert.Empty(visited["zero.txt"]);
        }

        [Fact]
        public void StreamHashPreservesLegacyPathAndContentDigest()
        {
            File.WriteAllText(Path.Combine(root, "a.txt"), "hello", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "b.txt"), "world", new UTF8Encoding(false));
            var a = SHA256.HashData(Encoding.UTF8.GetBytes("a.txthello"));
            var b = SHA256.HashData(Encoding.UTF8.GetBytes("b.txtworld"));
            var combined = new byte[64];
            a.CopyTo(combined, 0);
            b.CopyTo(combined, 32);
            var expected = Convert.ToHexString(SHA256.HashData(combined)).ToLowerInvariant();
            using var streamOnly = new StreamOnlyModFileProvider(root);
            Assert.Equal(expected, ModHasher.ComputeInstallSha256(streamOnly));
            Assert.Equal(expected, ModHasher.ComputeInstallSha256(root));
        }

        private void Visit(string path, Stream stream)
        {
            using var output = new MemoryStream();
            stream.CopyTo(output);
            visited.Add(path, output.ToArray());
        }
    }
}
