using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class ArchivePasswordStoreTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "archive_passwords_" + Guid.NewGuid().ToString("N"));
        public ArchivePasswordStoreTests() => Directory.CreateDirectory(root);
        public void Dispose() => Directory.Delete(root, true);

        [Fact]
        public void CredentialsSurviveJsonRoundTripAndCanBeRemoved()
        {
            var file = Path.Combine(root, "credentials.json");
            var archive = Path.Combine(root, "мод.7z");
            var another = Path.Combine(root, "another.7z");
            var store = new ArchivePasswordStore(file);
            Assert.Null(store.Get(archive));
            store.Set(archive, "пароль\\\"\n123");
            store.Set(another, "");
            var restored = new ArchivePasswordStore(file);
            Assert.Equal("пароль\\\"\n123", restored.Get(archive));
            Assert.Equal("", restored.Get(another));
            restored.Set(archive, null);
            Assert.Null(new ArchivePasswordStore(file).Get(archive));
            Assert.Equal("", new ArchivePasswordStore(file).Get(another));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }

        [Fact]
        public void BrokenCredentialFileIsNotOverwritten()
        {
            var file = Path.Combine(root, "credentials.json");
            File.WriteAllText(file, "broken");
            var store = new ArchivePasswordStore(file);
            Assert.Throws<System.Text.Json.JsonException>(OverwriteBrokenCredentials);
            Assert.Equal("broken", File.ReadAllText(file));
        }

        private void OverwriteBrokenCredentials() => new ArchivePasswordStore(Path.Combine(root, "credentials.json"))
            .Set(Path.Combine(root, "mod.7z"), "secret");
    }
}
