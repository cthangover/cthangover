namespace Engine.Tests.Mods
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using Cthangover.Core.Mods;

    /// <summary>Installation either publishes a fully verified new package or preserves the previous bytes.</summary>
    public class ModPackageInstallerTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "package_install_" + Guid.NewGuid().ToString("N"));
        private string archive;
        private string expectedHash;
        private string expectedId = "test_sevenzip";
        private string Target => Path.Combine(root, "installed");

        public ModPackageInstallerTests()
        {
            Directory.CreateDirectory(Target);
            File.WriteAllText(Path.Combine(Target, "previous.txt"), "previous working version");
        }

        public void Dispose() => Directory.Delete(root, true);

        [Theory]
        [InlineData("plain", null)]
        [InlineData("solid", null)]
        [InlineData("encrypted_headers_solid", SevenZipFixtures.Password)]
        public void Verified7zReplacesPreviousVersion(string fixture, string password)
        {
            archive = SevenZipFixtures.PathFor(fixture);
            using var provider = ArchiveUnlockService.OpenProvider(archive, password);
            var installHash = ModHasher.ComputeInstallSha256(provider);
            ModPackageInstaller.Install(archive, Target, expectedId, ModHasher.ComputeArchiveSha256(archive), installHash, password);
            Assert.False(File.Exists(Path.Combine(Target, "previous.txt")));
            Assert.Equal("Привет из 7z!\n", File.ReadAllText(Path.Combine(Target, "Data", "привет.txt")));
            Assert.Empty(Directory.GetDirectories(root, ".*"));
        }

        [Fact]
        public void ZipWrapperIsFlattenedAndVerifiedBeforePublication()
        {
            archive = Path.Combine(root, "download.package");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                WriteZip(zip, "wrapper/manifest.json", "{\"id\":\"test_sevenzip\",\"categories\": [\"library\"]}");
                WriteZip(zip, "wrapper/data.txt", "new version");
            }
            ModPackageInstaller.Install(archive, Target, expectedId);
            Assert.Equal("new version", File.ReadAllText(Path.Combine(Target, "data.txt")));
            Assert.False(Directory.Exists(Path.Combine(Target, "wrapper")));
        }

        [Theory]
        [InlineData("archive")]
        [InlineData("install")]
        [InlineData("manifest")]
        public void VerificationFailurePreservesOldInstallation(string failure)
        {
            archive = SevenZipFixtures.PathFor("plain");
            expectedHash = failure == "install" ? new string('0', 64) : null;
            if (failure == "manifest")
                expectedId = "different_id";
            var error = failure == "archive" ? Record.Exception(InstallBadArchiveHash) : Record.Exception(InstallCurrent);
            Assert.NotNull(error);
            Assert.Equal("previous working version", File.ReadAllText(Path.Combine(Target, "previous.txt")));
            Assert.False(File.Exists(Path.Combine(Target, "manifest.json")));
            Assert.Empty(Directory.GetDirectories(root, ".*"));
        }

        [Theory]
        [InlineData("../escaped.txt")]
        [InlineData("C:/escaped.txt")]
        [InlineData("/escaped.txt")]
        public void UnsafeEntriesCannotEscapeStaging(string entry)
        {
            archive = Path.Combine(root, "unsafe.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
                WriteZip(zip, entry, "unsafe");
            Assert.NotNull(Record.Exception(InstallCurrent));
            Assert.Equal("previous working version", File.ReadAllText(Path.Combine(Target, "previous.txt")));
            Assert.False(File.Exists(Path.Combine(root, "escaped.txt")));
            Assert.Empty(Directory.GetDirectories(root, ".*"));
        }

        [Fact]
        public void WrongPasswordPreservesPreviousVersion()
        {
            archive = SevenZipFixtures.PathFor("encrypted_headers_solid");
            Assert.NotNull(Record.Exception(InstallCurrent));
            Assert.Equal("previous working version", File.ReadAllText(Path.Combine(Target, "previous.txt")));
        }

        [Theory]
        [InlineData("encrypted_aes.zip")]
        [InlineData("encrypted_pkware.zip")]
        public void EncryptedZipIsVerifiedAndInstalled(string file)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sevenzip", file);
            ModPackageInstaller.Install(path, Target, expectedId, password: "fixture-password");
            Assert.Equal("Привет из 7z!\n", File.ReadAllText(Path.Combine(Target, "Data", "привет.txt")));
        }

        [Fact]
        public void CaseAmbiguousPathsAreRejectedOnBothOperatingSystems()
        {
            archive = Path.Combine(root, "case_collision.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                WriteZip(zip, "A.txt", "one");
                WriteZip(zip, "a.txt", "two");
            }
            Assert.NotNull(Record.Exception(InstallCurrent));
            Assert.Equal("previous working version", File.ReadAllText(Path.Combine(Target, "previous.txt")));
        }

        private void InstallCurrent() => ModPackageInstaller.Install(archive, Target, expectedId, installSha256: expectedHash);
        private void InstallBadArchiveHash() => ModPackageInstaller.Install(archive, Target, expectedId, new string('0', 64));

        private static void WriteZip(ZipArchive zip, string path, string value)
        {
            using var writer = new StreamWriter(zip.CreateEntry(path).Open());
            writer.Write(value);
        }
    }
}
