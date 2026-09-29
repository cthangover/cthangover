using System.Text.Json;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Patches;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    public class CollectPatchesTests
    {
        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        [Fact]
        public void ListFiles_FlatFile_NotFoundAsDirectory()
        {
            var provider = new MockModFileProvider("test_flat_mod");
            provider.AddFile("patches/character.json", "{}");

            var result = provider.ListFiles("patches/character").ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void ListFiles_FlatFile_FoundInPatchesRoot()
        {
            var provider = new MockModFileProvider("test_flat_mod");
            provider.AddFile("patches/character.json", "{}");

            var result = provider.ListFiles("patches").ToList();

            Assert.Contains("patches/character.json", result);
        }

        [Fact]
        public void ListFiles_Directory_FoundCorrectly()
        {
            var provider = new MockModFileProvider("test_dir_mod");
            provider.AddFile("patches/character/marao.json", "{}");

            var result = provider.ListFiles("patches/character").ToList();

            Assert.Contains("patches/character/marao.json", result);
        }

        [Fact]
        public void ListFiles_Directory_NotFoundInPatchesRoot()
        {
            var provider = new MockModFileProvider("test_dir_mod");
            provider.AddFile("patches/character/marao.json", "{}");

            var result = provider.ListFiles("patches").ToList();

            Assert.Contains("patches/character/", result);
            Assert.DoesNotContain("patches/character/marao.json", result);
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

            var merger = new JsonPatchMerger();
            var mergedJson = merger.Merge(maraoJson, "Marao", patches);

            var result = JsonSerializer.Deserialize<CharacterInfo>(mergedJson, ReadOptions);

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

            var merger = new JsonPatchMerger();
            var mergedJson = merger.Merge(maraoJson, "Marao", null);

            var result = JsonSerializer.Deserialize<CharacterInfo>(mergedJson, ReadOptions);

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

            var merger = new JsonPatchMerger();
            var mergedJson = merger.Merge(maraoJson, "Marao", patches);

            var result = JsonSerializer.Deserialize<CharacterInfo>(mergedJson, ReadOptions);

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

            var merger = new JsonPatchMerger();
            var mergedJson = merger.Merge(maraoJson, "Marao", patches);

            var result = JsonSerializer.Deserialize<CharacterInfo>(mergedJson, ReadOptions);

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
