using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Providers;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Tests the collection store: collections are packages (a folder or zip
    /// with <c>collection.json</c>, cover and <c>locale/</c>); the player's
    /// packages persist and survive round-trips, a fork takes its source
    /// package's cover and texts along, broken packages never throw.
    /// </summary>
    public class ModCollectionStoreTests : IDisposable
    {
        private readonly string _scratch = Path.Combine(Path.GetTempPath(), "collection_store_" + Guid.NewGuid().ToString("N"));

        public ModCollectionStoreTests()
        {
            ClearUser();
            Directory.CreateDirectory(_scratch);
        }

        public void Dispose()
        {
            ClearUser();
            try
            {
                Directory.Delete(_scratch, true);
            }
            catch
            {
            }
        }

        private static void ClearUser()
        {
            if (Directory.Exists(ModCollectionStore.UserPath))
                Directory.Delete(ModCollectionStore.UserPath, true);
        }

        private string Package(string name, string json)
        {
            var dir = Path.Combine(_scratch, name);
            Directory.CreateDirectory(dir);
            if (json != null)
                File.WriteAllText(Path.Combine(dir, ModCollectionStore.DocumentName), json);
            return dir;
        }

        [Fact]
        public void LoadUser_NoFolder_ReturnsEmpty()
        {
            Assert.Empty(ModCollectionStore.LoadUser());
        }

        [Fact]
        public void SaveUser_ThenLoadUser_Roundtrip()
        {
            var original = new List<ModCollection>
            {
                new()
                {
                    Id = "my_novel",
                    Name = "My Novel",
                    Version = "1.0.0",
                    Mechanics = new List<ModCollectionMechanic>
                    {
                        new() { Id = "core", Name = "Core", Mods = new List<string> { "cthangover_story>=1" } },
                        new() { Id = "cards", Mods = new List<string> { "card_battle" }, Optional = true },
                    },
                },
                new() { Id = "tools", Name = "Tools" },
            };

            ModCollectionStore.SaveUser(original);
            var loaded = ModCollectionStore.LoadUser();

            Assert.Equal(2, loaded.Count);
            var novel = loaded.Find(c => c.Id == "my_novel");
            Assert.NotNull(novel);
            Assert.Equal("My Novel", novel.Name);
            Assert.Equal("1.0.0", novel.Version);
            Assert.Equal(2, novel.Mechanics.Count);
            Assert.Equal("core", novel.Mechanics[0].Id);
            Assert.Equal("cthangover_story>=1", novel.Mechanics[0].Mods[0]);
            Assert.True(novel.Mechanics[1].Optional);
            Assert.All(loaded, c => Assert.Equal(ModCollectionOrigin.User, c.Origin));
            Assert.All(loaded, c => Assert.NotNull(c.Package));
            Assert.True(File.Exists(Path.Combine(ModCollectionStore.UserPath, "my_novel", ModCollectionStore.DocumentName)));
        }

        [Fact]
        public void SaveUser_DropsPackagesMissingFromTheList()
        {
            ModCollectionStore.SaveUser(new List<ModCollection> { new() { Id = "a" }, new() { Id = "b" } });

            ModCollectionStore.SaveUser(new List<ModCollection> { new() { Id = "b" } });

            var loaded = Assert.Single(ModCollectionStore.LoadUser());
            Assert.Equal("b", loaded.Id);
            Assert.False(Directory.Exists(Path.Combine(ModCollectionStore.UserPath, "a")));
        }

        [Fact]
        public void SaveUser_Null_LeavesNoPackages()
        {
            ModCollectionStore.SaveUser(new List<ModCollection> { new() { Id = "a" } });

            ModCollectionStore.SaveUser(null);

            Assert.Empty(ModCollectionStore.LoadUser());
        }

        [Fact]
        public void SaveUser_ForkCopiesCoverAndLocaleOfTheSourcePackage()
        {
            var source = Package("shipped", "{ \"id\": \"shipped\" }");
            File.WriteAllBytes(Path.Combine(source, "cover.png"), new byte[] { 1, 2, 3 });
            Directory.CreateDirectory(Path.Combine(source, "locale"));
            File.WriteAllText(Path.Combine(source, "locale", "ru.properties"), "collections/shipped/name = Игра");
            var fork = new ModCollection { Id = "fork", Cover = "cover.png", Package = new FolderModFileProvider(source, "shipped") };

            ModCollectionStore.SaveUser(new List<ModCollection> { fork });

            var dir = Path.Combine(ModCollectionStore.UserPath, "fork");
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(dir, "cover.png")));
            Assert.Equal("collections/shipped/name = Игра", File.ReadAllText(Path.Combine(dir, "locale", "ru.properties")));
            var loaded = Assert.Single(ModCollectionStore.LoadUser());
            Assert.Equal(new byte[] { 1, 2, 3 }, loaded.Package.ReadFileBinary(loaded.Cover));
        }

        [Fact]
        public void RemoveUser_DeletesThePackage()
        {
            ModCollectionStore.SaveUser(new List<ModCollection> { new() { Id = "gone" } });

            Assert.True(ModCollectionStore.RemoveUser("gone"));
            Assert.False(ModCollectionStore.RemoveUser("gone"));
            Assert.Empty(ModCollectionStore.LoadUser());
        }

        [Fact]
        public void LoadPackages_ReadsFoldersAndZips()
        {
            Package("folder_game", "{ \"id\": \"folder_game\", \"name\": \"Folder\", \"mechanics\": [ { \"id\": \"m\", \"mods\": [\"card_battle>=1\"] } ] }");
            using (var zip = ZipFile.Open(Path.Combine(_scratch, "zip_game.zip"), ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry(ModCollectionStore.DocumentName).Open());
                writer.Write("{ \"id\": \"zip_game\", \"name\": \"Zip\" }");
            }

            var result = ModCollectionStore.LoadPackages(_scratch, ModCollectionOrigin.BuiltIn);

            Assert.Equal(2, result.Count);
            var folder = result.Find(c => c.Id == "folder_game");
            Assert.Equal("card_battle>=1", folder.Mechanics[0].Mods[0]);
            Assert.Equal(ModCollectionOrigin.BuiltIn, folder.Origin);
            Assert.Equal("Zip", result.Find(c => c.Id == "zip_game").Name);
        }

        [Fact]
        public void LoadPackages_SkipsBrokenAndForeignEntries()
        {
            Package("broken", "{ broken ]");
            Package("no_id", "{ \"name\": \"anonymous\" }");
            Package("empty_folder", null);
            File.WriteAllText(Path.Combine(_scratch, "notes.txt"), "not a package");
            Package("good", "{ \"id\": \"good\", \"name\": \"Good\" }");

            var result = ModCollectionStore.LoadPackages(_scratch, ModCollectionOrigin.User);

            var collection = Assert.Single(result);
            Assert.Equal("good", collection.Id);
        }

        [Fact]
        public void LoadPackages_MissingRoot_ReturnsEmpty()
        {
            Assert.Empty(ModCollectionStore.LoadPackages(Path.Combine(_scratch, "absent"), ModCollectionOrigin.User));
            Assert.Empty(ModCollectionStore.LoadPackages(null, ModCollectionOrigin.User));
        }
    }
}
