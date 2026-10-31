namespace Engine.Tests.Mods
{
    using System;
    using System.IO;
    using Cthangover.Core.Mods;

    /// <summary>Credential acceptance follows real decryption; session credentials never touch the durable store.</summary>
    public class ArchiveUnlockServiceTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "archive_unlock_" + Guid.NewGuid().ToString("N"));
        private string path;
        private ArchivePasswordStore store;
        private ArchiveUnlockService service;

        public ArchiveUnlockServiceTests()
        {
            Directory.CreateDirectory(root);
            store = new ArchivePasswordStore(Path.Combine(root, "passwords.json"));
            service = new ArchiveUnlockService(store);
        }

        public void Dispose() => Directory.Delete(root, true);

        [Theory]
        [InlineData("encrypted_headers_solid.7z", SevenZipFixtures.Password)]
        [InlineData("encrypted_aes.zip", "fixture-password")]
        [InlineData("encrypted_pkware.zip", "fixture-password")]
        public void UnlockUsesSessionCredentialsUnlessExplicitlyRemembered(string file, string password)
        {
            path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sevenzip", file);
            service.RecordFailure(path, true);
            Assert.Equal(Path.GetFullPath(path), Assert.Single(service.GetUnavailable()).Path);
            Assert.Equal(Path.GetFileName(path), Assert.Single(service.GetUnavailable()).Name);
            Assert.True(Assert.Single(service.GetUnavailable()).NeedsPassword);
            Assert.Equal("test_sevenzip", service.Unlock(path, password));
            Assert.Empty(service.GetUnavailable());
            Assert.Equal(password, store.Get(path));
            Assert.False(File.Exists(Path.Combine(root, "passwords.json")));
            Assert.Null(new ArchivePasswordStore(Path.Combine(root, "passwords.json")).Get(path));
            service.Unlock(path, password, remember: true);
            Assert.Equal(password, new ArchivePasswordStore(Path.Combine(root, "passwords.json")).Get(path));
        }

        [Theory]
        [InlineData("encrypted_headers_solid.7z")]
        [InlineData("encrypted_aes.zip")]
        [InlineData("encrypted_pkware.zip")]
        public void WrongPasswordKeepsPackageLockedAndDoesNotPersist(string file)
        {
            path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sevenzip", file);
            service.RecordFailure(path, true);
            Assert.NotNull(Record.Exception(UnlockWrongPassword));
            Assert.Single(service.GetUnavailable());
            Assert.Null(store.Get(path));
            Assert.False(File.Exists(Path.Combine(root, "passwords.json")));
        }

        private void UnlockWrongPassword() => service.Unlock(path, "wrong-password", remember: true);
    }
}
