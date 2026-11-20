using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Patches;
using Cthangover.Core.Mods.Providers;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Patch-discovery contract exercised against the REAL storage providers
    /// (folder and zip), the way the patch collector consumes them: a flat
    /// patch file is not a directory, directory entries carry the "/" suffix
    /// and nested patch files are listed under their directory.
    /// </summary>
    public class CollectPatchesTests : IDisposable
    {
        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private readonly string _testDir = Path.Combine(
            Path.GetTempPath(), $"collect_patches_test_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        private const string Manifest = "{ \"id\": \"patch_mod\" }";
        private const string PatchBody = "{ \"Items\": [] }";

        [Fact]
        public void ListFiles_FlatFile_NotFoundAsDirectory()
        {
            using var provider = CreateFolderProvider();
            using var zipProvider = CreateZipProvider();

            // "patches/character" is a flat file: asking for its listing must
            // return nothing — real providers must not treat files as dirs.
            Assert.Empty(provider.ListFiles("patches/character.json").ToList());
            Assert.Empty(zipProvider.ListFiles("patches/character.json").ToList());
        }

        [Fact]
        public void ListFiles_FlatFile_FoundInPatchesRoot()
        {
            using var provider = CreateFolderProvider();
            using var zipProvider = CreateZipProvider();

            Assert.Contains("patches/character.json", provider.ListFiles("patches").ToList());
            Assert.Contains("patches/character.json", zipProvider.ListFiles("patches").ToList());
        }

        [Fact]
        public void ListFiles_Directory_FoundCorrectly()
        {
            using var provider = CreateFolderProvider();
            using var zipProvider = CreateZipProvider();

            Assert.Contains("patches/character/marao.json", provider.ListFiles("patches/character").ToList());
            Assert.Contains("patches/character/marao.json", zipProvider.ListFiles("patches/character").ToList());
        }

        [Fact]
        public void ListFiles_Directory_NotFoundInPatchesRoot()
        {
            using var provider = CreateFolderProvider();
            using var zipProvider = CreateZipProvider();

            // The directory itself is announced with the "/" suffix so the
            // recursive collector can descend into it without stat-calls, and
            // its files are never flattened into the parent listing.
            Assert.Contains("patches/character/", provider.ListFiles("patches").ToList());
            Assert.DoesNotContain("patches/character/marao.json", provider.ListFiles("patches").ToList());
            Assert.Contains("patches/character/", zipProvider.ListFiles("patches").ToList());
            Assert.DoesNotContain("patches/character/marao.json", zipProvider.ListFiles("patches").ToList());
        }

        /// <summary>Builds the shared patch layout on disk: manifest, flat patch file and a directory patch.</summary>
        private string CreatePatchTree()
        {
            var root = Path.Combine(_testDir, "patch_mod");
            Directory.CreateDirectory(Path.Combine(root, "patches", "character"));
            File.WriteAllText(Path.Combine(root, "manifest.json"), Manifest);
            File.WriteAllText(Path.Combine(root, "patches", "character.json"), PatchBody);
            File.WriteAllText(Path.Combine(root, "patches", "character", "marao.json"), PatchBody);
            return root;
        }

        private FolderModFileProvider CreateFolderProvider()
        {
            return new FolderModFileProvider(CreatePatchTree(), "patch_mod_folder");
        }

        private ZipModFileProvider CreateZipProvider()
        {
            var root = CreatePatchTree();
            var zipPath = Path.Combine(_testDir, "patch_mod.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "manifest.json", Manifest);
                WriteEntry(archive, "patches/character.json", PatchBody);
                WriteEntry(archive, "patches/character/marao.json", PatchBody);
            }

            return new ZipModFileProvider(zipPath, "patch_mod_zip");
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        [Fact]
        public void Merge_MaraoOverride_HealthAndAttackPatched()
        {
            var maraoJson = @"{
                ""Id"": ""Marao"",
                ""Name"": ""characters/player/marao"",
                ""Health"": 16,
                ""Attack"": 5,
                ""Point"": 1,
                ""Actions"": [""physics/attack"", ""physics/defence""]
            }";

            var patch = new PropertyPatch
            {
                ID = "Marao",
                ExtraProperties = new Dictionary<string, object>
                {
                    ["Health"] = JsonSerializer.Deserialize<JsonElement>("5000"),
                    ["Attack"] = JsonSerializer.Deserialize<JsonElement>("1200"),
                    ["Point"] = JsonSerializer.Deserialize<JsonElement>("2"),
                    ["Actions$add"] = JsonSerializer.Deserialize<JsonElement>(@"[""physics/stun""]")
                }
            };

            var patches = new Dictionary<string, List<PropertyPatch>>
            {
                ["Marao"] = new() { patch }
            };

            var mergedJson = JsonPatchMerger.Merge(maraoJson, "Marao", patches);

            var result = JsonSerializer.Deserialize<CharacterPatchSample>(mergedJson, ReadOptions);

            Assert.Equal(5000, result.Health);
            Assert.Equal(1200, result.Attack);
            Assert.Equal(2, result.Point);
            Assert.Contains("physics/stun", result.Actions);
            Assert.Contains("physics/attack", result.Actions);
            Assert.Contains("physics/defence", result.Actions);
            Assert.Equal(3, result.Actions.Count);
        }

        [Fact]
        public void Merge_WithoutPatch_KeepsOriginalValues()
        {
            var maraoJson = @"{
                ""Id"": ""Marao"",
                ""Health"": 16,
                ""Attack"": 5,
                ""Point"": 1,
                ""Actions"": [""physics/attack"", ""physics/defence""]
            }";

            var mergedJson = JsonPatchMerger.Merge(maraoJson, "Marao", null);

            var result = JsonSerializer.Deserialize<CharacterPatchSample>(mergedJson, ReadOptions);

            Assert.Equal(16, result.Health);
            Assert.Equal(5, result.Attack);
            Assert.Equal(1, result.Point);
            Assert.Equal(2, result.Actions.Count);
        }

        [Fact]
        public void Merge_ActionsAdd_Deduplicates()
        {
            var maraoJson = @"{
                ""Id"": ""Marao"",
                ""Actions"": [""physics/attack"", ""physics/defence""]
            }";

            var patch = new PropertyPatch
            {
                ID = "Marao",
                ExtraProperties = new Dictionary<string, object>
                {
                    ["Actions$add"] = JsonSerializer.Deserialize<JsonElement>(@"[""physics/attack"", ""physics/stun""]")
                }
            };

            var patches = new Dictionary<string, List<PropertyPatch>>
            {
                ["Marao"] = new() { patch }
            };

            var mergedJson = JsonPatchMerger.Merge(maraoJson, "Marao", patches);

            var result = JsonSerializer.Deserialize<CharacterPatchSample>(mergedJson, ReadOptions);

            Assert.Contains("physics/attack", result.Actions);
            Assert.Contains("physics/defence", result.Actions);
            Assert.Contains("physics/stun", result.Actions);
        }

        [Fact]
        public void Merge_ActionsRemove_RemovesSpecific()
        {
            var maraoJson = @"{
                ""Id"": ""Marao"",
                ""Actions"": [""physics/attack"", ""physics/defence"", ""physics/stun""]
            }";

            var patch = new PropertyPatch
            {
                ID = "Marao",
                ExtraProperties = new Dictionary<string, object>
                {
                    ["Actions$remove"] = JsonSerializer.Deserialize<JsonElement>(@"[""physics/defence""]")
                }
            };

            var patches = new Dictionary<string, List<PropertyPatch>>
            {
                ["Marao"] = new() { patch }
            };

            var mergedJson = JsonPatchMerger.Merge(maraoJson, "Marao", patches);

            var result = JsonSerializer.Deserialize<CharacterPatchSample>(mergedJson, ReadOptions);

            Assert.Contains("physics/attack", result.Actions);
            Assert.Contains("physics/stun", result.Actions);
            Assert.DoesNotContain("physics/defence", result.Actions);
            Assert.Equal(2, result.Actions.Count);
        }

        [Fact]
        public void Deserialize_PatchFile_RoundTrip()
        {
            var json = @"{
                ""Items"": [
                    {
                        ""Id"": ""Marao"",
                        ""Health"": 5000,
                        ""Attack"": 1200,
                        ""Point"": 2,
                        ""Actions$add"": [""physics/stun""]
                    }
                ]
            }";

            var fileData = JsonSerializer.Deserialize<FileData<PropertyPatch>>(json, ReadOptions);

            Assert.NotNull(fileData);
            Assert.Single(fileData.Items);
            Assert.Equal("Marao", fileData.Items[0].ID);

            var props = fileData.Items[0].ExtraProperties;
            Assert.NotNull(props);
            Assert.Contains("Health", props.Keys);
            Assert.Contains("Attack", props.Keys);
            Assert.Contains("Point", props.Keys);
            Assert.Contains("Actions$add", props.Keys);
        }
    }
}
