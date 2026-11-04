using System;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>Persistence, shipped-source overrides and key ownership through the actual player store.</summary>
    public sealed class CatalogServerStoreTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "catalog-servers-" + Guid.NewGuid().ToString("N"));
        private readonly ModConfig config = new();
        private readonly CatalogServerStore store;
        private CatalogServer candidate;
        private string secret;

        public CatalogServerStoreTests()
        {
            Directory.CreateDirectory(root);
            store = new CatalogServerStore(Path.Combine(root, "catalog_servers.json"));
        }

        [Fact]
        public void SavedSourcesAndKeysRoundTripAndDisabledSourcesStayDisabled()
        {
            var entry = new CatalogServer { Name = "Local test", Url = "127.0.0.1:18080", Enabled = false };
            var token = new string('A', 1024);
            var saved = store.Upsert(config, entry, token);
            var reloaded = new CatalogServerStore(Path.Combine(root, "catalog_servers.json")).Load(config);
            var actual = Assert.Single(reloaded);
            Assert.Equal(entry.Id, actual.Id);
            Assert.Equal(entry.Name, actual.Name);
            Assert.Equal("http://127.0.0.1:18080/v1/catalog", actual.Url);
            Assert.False(actual.Enabled);
            Assert.Equal(saved.TokenFile, actual.TokenFile);
            Assert.Equal(token, File.ReadAllText(actual.TokenFile).Trim());
            Assert.DoesNotContain(token, File.ReadAllText(Path.Combine(root, "catalog_servers.json")));
            store.ApplyTo(config);
            Assert.Empty(config.CatalogUrls);
            Assert.Empty(config.CatalogTokenFiles);
            actual.Enabled = true;
            store.Upsert(config, actual);
            store.ApplyTo(config);
            Assert.Equal(actual.Url, Assert.Single(config.CatalogUrls));
            Assert.Equal(actual.TokenFile, config.CatalogTokenFiles[actual.Url]);
        }

        [Fact]
        public void ShippedCatalogCanBeEditedAndRemovingTheLastDoesNotRestoreDefaults()
        {
            config.CatalogUrls.Add("https://public.example/catalog.json");
            var inherited = Assert.Single(store.Load(config));
            inherited.Name = "Edited default";
            inherited.Enabled = false;
            store.Upsert(config, inherited);
            var updated = Assert.Single(store.Load(config));
            Assert.Equal(inherited.Id, updated.Id);
            Assert.Equal("Edited default", updated.Name);
            Assert.False(updated.Enabled);
            store.Remove(config, inherited.Id);
            Assert.Empty(store.Load(config));
            store.ApplyTo(config);
            Assert.Empty(config.CatalogUrls);
            var otherConfig = new ModConfig();
            otherConfig.CatalogUrls.Add("https://public.example/catalog.json");
            store.ApplyTo(otherConfig);
            Assert.Empty(otherConfig.CatalogUrls);
        }

        [Fact]
        public void KeyReplacementAndEndpointChangeNeverReuseOrLeakOldKeys()
        {
            var saved = store.Upsert(config, new CatalogServer { Url = "https://private.example" }, new string('A', 1024));
            var kept = store.Upsert(config, saved);
            Assert.Equal(saved.TokenFile, kept.TokenFile);
            var replaced = store.Upsert(config, saved, new string('B', 1024));
            Assert.NotEqual(saved.TokenFile, replaced.TokenFile);
            Assert.False(File.Exists(saved.TokenFile));
            Assert.Equal(new string('B', 1024), File.ReadAllText(replaced.TokenFile).Trim());
            replaced.Url = "https://another.example";
            var moved = store.Upsert(config, replaced);
            Assert.Null(moved.TokenFile);
            Assert.False(File.Exists(replaced.TokenFile));
        }

        [Fact]
        public void RemovingASourceDeletesOnlyOwnedKeyFiles()
        {
            var borrowed = Path.Combine(root, "owner.token");
            File.WriteAllText(borrowed, new string('A', 1024));
            config.CatalogUrls.Add("https://borrowed.example/v1/catalog");
            config.CatalogTokenFiles.Add(config.CatalogUrls[0], borrowed);
            var inherited = Assert.Single(store.Load(config));
            store.Remove(config, inherited.Id);
            Assert.True(File.Exists(borrowed));
            var owned = store.Upsert(config, new CatalogServer { Url = "https://owned.example" }, new string('B', 1024));
            store.Remove(config, owned.Id);
            Assert.False(File.Exists(owned.TokenFile));
        }

        [Fact]
        public void ClearKeyConvertsTheCatalogToPublic()
        {
            var saved = store.Upsert(config, new CatalogServer { Url = "https://private.example" }, new string('A', 1024));
            var cleared = store.Upsert(config, saved, clearToken: true);
            Assert.Null(cleared.TokenFile);
            Assert.False(File.Exists(saved.TokenFile));
            store.ApplyTo(config);
            Assert.Single(config.CatalogUrls);
            Assert.Empty(config.CatalogTokenFiles);
        }

        [Theory]
        [InlineData("127.0.0.1:18080", "http://127.0.0.1:18080/v1/catalog")]
        [InlineData("https://example.com/", "https://example.com/v1/catalog")]
        [InlineData("https://example.com/catalog.json", "https://example.com/catalog.json")]
        public void ServerAddressesAndCatalogDocumentsAreAccepted(string value, string expected)
            => Assert.Equal(expected, CatalogServerStore.NormalizeUrl(value));

        [Theory]
        [InlineData("file:///tmp/catalog.json")]
        [InlineData("https://user:secret@example.com/catalog.json")]
        [InlineData("https://example.com/catalog.json?token=secret")]
        public void UnsafeAddressesDoNotCreateAStoredSource(string url)
        {
            candidate = new CatalogServer { Url = url };
            Assert.Throws<ArgumentException>(SaveCandidate);
            Assert.Empty(store.Load(config));
        }

        [Fact]
        public void DuplicateUrlAndInvalidSecretDoNotReplaceExistingData()
        {
            var saved = store.Upsert(config, new CatalogServer { Url = "https://example.com" });
            candidate = new CatalogServer { Url = "https://example.com/" };
            Assert.Throws<InvalidOperationException>(SaveCandidate);
            candidate = saved;
            secret = "invalid-secret";
            var error = Assert.Throws<ArgumentException>(SaveCandidate);
            Assert.DoesNotContain(secret, error.Message);
            Assert.Null(Assert.Single(store.Load(config)).TokenFile);
        }

        [Fact]
        public void BrokenPlayerFileIsNotOverwritten()
        {
            var file = Path.Combine(root, "catalog_servers.json");
            File.WriteAllText(file, "broken JSON");
            candidate = new CatalogServer { Url = "https://example.com" };
            Assert.Throws<JsonException>(SaveCandidate);
            Assert.Equal("broken JSON", File.ReadAllText(file));
        }

        [Fact]
        public void PrivateKeyCannotBeSavedForRemotePlaintextHttp()
        {
            candidate = new CatalogServer { Url = "http://remote.example/v1/catalog" };
            secret = new string('A', 1024);
            Assert.Throws<ArgumentException>(SaveCandidate);
            Assert.Empty(store.Load(config));
            Assert.False(Directory.Exists(Path.Combine(root, "catalog_keys")));
        }

        private void SaveCandidate() => store.Upsert(config, candidate, secret);
        public void Dispose() => Directory.Delete(root, true);
    }
}
