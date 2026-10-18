using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Mods;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the collection store: player files persist and survive round-trips,
    /// broken files never throw, and bundled <c>collections/*.json</c> files are
    /// discovered through <see cref="IModFileProvider"/> so both folder and zip
    /// mods work identically.
    /// </summary>
    public class ModCollectionStoreTests : IDisposable
    {
        private const string TestFile = "collections.json";

        public void Dispose()
        {
            try
            {
                if (File.Exists(TestFile))
                    File.Delete(TestFile);
            }
            catch
            {
            }
        }

        [Fact]
        public void LoadUser_NoFile_ReturnsEmpty()
        {
            if (File.Exists(TestFile))
                File.Delete(TestFile);

            var result = ModCollectionStore.LoadUser();

            Assert.Empty(result);
        }

        [Fact]
        public void SaveUser_ThenLoadUser_Roundtrip()
        {
            var original = new List<ModCollection>
            {
                new()
                {
                    Id = "card_novel",
                    Name = "Card Novel",
                    Version = "1.0.0",
                    Mechanics = new List<ModCollectionMechanic>
                    {
                        new()
                        {
                            Id = "core",
                            Name = "Core",
                            Mods = new List<string> { "cthangover_story>=1" },
                        },
                        new()
                        {
                            Id = "cards",
                            Mods = new List<string> { "card_battle" },
                            Optional = true,
                        },
                    },
                },
                new() { Id = "tools", Name = "Tools" },
            };

            ModCollectionStore.SaveUser(original);
            var loaded = ModCollectionStore.LoadUser();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("card_novel", loaded[0].Id);
            Assert.Equal("Card Novel", loaded[0].Name);
            Assert.Equal("1.0.0", loaded[0].Version);
            Assert.Equal(2, loaded[0].Mechanics.Count);
            Assert.Equal("core", loaded[0].Mechanics[0].Id);
            Assert.Equal("cthangover_story>=1", loaded[0].Mechanics[0].Mods[0]);
            Assert.True(loaded[0].Mechanics[1].Optional);
            Assert.All(loaded, c => Assert.Equal(ModCollectionOrigin.User, c.Origin));
        }

        [Fact]
        public void SaveUser_Null_WritesEmptyListFile()
        {
            ModCollectionStore.SaveUser(null);

            Assert.True(File.Exists(TestFile));
            Assert.Empty(ModCollectionStore.LoadUser());
        }

        [Fact]
        public void LoadUser_InvalidJson_ReturnsEmpty()
        {
            File.WriteAllText(TestFile, "{ not a list }}}");

            var result = ModCollectionStore.LoadUser();

            Assert.Empty(result);
        }

        [Fact]
        public void LoadUser_EntryWithoutId_IsSkipped()
        {
            var json = """
            [
              { "name": "No id" },
              { "id": "valid", "name": "Valid" }
            ]
            """;
            File.WriteAllText(TestFile, json);

            var result = ModCollectionStore.LoadUser();

            var collection = Assert.Single(result);
            Assert.Equal("valid", collection.Id);
        }

        [Fact]
        public void LoadFromProvider_Null_ReturnsEmpty()
        {
            Assert.Empty(ModCollectionStore.LoadFromProvider(null));
        }

        [Fact]
        public void LoadFromProvider_ReadsBundledCollection()
        {
            var provider = new MockModFileProvider("card_battle");
            provider.AddFile("manifest.json", "{}");
            provider.AddFile("collections/card_novel.json", """
            {
              "id": "card_novel",
              "name": "Card Novel",
              "mechanics": [
                { "id": "cards", "name": "Cards", "mods": ["card_battle>=1"] }
              ]
            }
            """);

            var result = ModCollectionStore.LoadFromProvider(provider);

            var collection = Assert.Single(result);
            Assert.Equal("card_novel", collection.Id);
            Assert.Equal(ModCollectionOrigin.BuiltIn, collection.Origin);
            Assert.Equal("card_battle>=1", collection.Mechanics[0].Mods[0]);
        }

        [Fact]
        public void LoadFromProvider_SkipsBrokenAndNonJsonFiles()
        {
            var provider = new MockModFileProvider("pack");
            provider.AddFile("collections/notes.txt", "not a collection");
            provider.AddFile("collections/broken.json", "{ broken ]");
            provider.AddFile("collections/no_id.json", "{ \"name\": \"anonymous\" }");
            provider.AddFile("collections/good.json", "{ \"id\": \"good\", \"name\": \"Good\" }");

            var result = ModCollectionStore.LoadFromProvider(provider);

            var collection = Assert.Single(result);
            Assert.Equal("good", collection.Id);
        }

        [Fact]
        public void LoadFromProvider_NoCollectionsFolder_ReturnsEmpty()
        {
            var provider = new MockModFileProvider("plain");
            provider.AddFile("manifest.json", "{}");

            Assert.Empty(ModCollectionStore.LoadFromProvider(provider));
        }
    }
}
