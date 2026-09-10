using System.Text.Json;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class PropertyPatchTests
    {
        [Fact]
        public void PropertyPatch_RoundTrip_PreservesProperties()
        {
            var patch = new PropertyPatch
            {
                ID = "TestEntity",
                ExtraProperties = new Dictionary<string, object>
                {
                    { "IntProp", JsonSerializer.Deserialize<JsonElement>("42") },
                    { "StrProp", JsonSerializer.Deserialize<JsonElement>("\"hello\"") },
                    { "BoolProp", JsonSerializer.Deserialize<JsonElement>("true") },
                    { "FloatProp", JsonSerializer.Deserialize<JsonElement>("3.14") }
                }
            };

            var json = JsonSerializer.Serialize(patch);
            var restored = JsonSerializer.Deserialize<PropertyPatch>(json);

            Assert.Equal("TestEntity", restored.ID);
            Assert.NotNull(restored.ExtraProperties);

            var intVal = Assert.IsType<JsonElement>(restored.ExtraProperties["IntProp"]);
            Assert.Equal(42, intVal.GetInt32());

            var strVal = Assert.IsType<JsonElement>(restored.ExtraProperties["StrProp"]);
            Assert.Equal("hello", strVal.GetString());

            var boolVal = Assert.IsType<JsonElement>(restored.ExtraProperties["BoolProp"]);
            Assert.True(boolVal.GetBoolean());

            var floatVal = Assert.IsType<JsonElement>(restored.ExtraProperties["FloatProp"]);
            Assert.Equal(3.14, floatVal.GetDouble());
        }

        [Fact]
        public void PropertyPatch_WildcardId_PreservesAsterisk()
        {
            var patch = new PropertyPatch
            {
                ID = "*",
                ExtraProperties = new Dictionary<string, object>
                {
                    { "GlobalProp", JsonSerializer.Deserialize<JsonElement>("1") }
                }
            };

            var json = JsonSerializer.Serialize(patch);
            var restored = JsonSerializer.Deserialize<PropertyPatch>(json);

            Assert.Equal("*", restored.ID);
        }

        [Fact]
        public void PropertyPatch_EmptyExtraProperties_IsNullByDefault()
        {
            var patch = new PropertyPatch { ID = "Test" };
            var json = JsonSerializer.Serialize(patch);
            var restored = JsonSerializer.Deserialize<PropertyPatch>(json);

            Assert.Equal("Test", restored.ID);
            Assert.Null(restored.ExtraProperties);
        }

        [Fact]
        public void PropertyPatch_FileDataDeserialization_Works()
        {
            var json = @"
            {
                ""Items"": [
                    { ""Id"": ""Hero"", ""Strength"": 15, ""Magic"": 5 },
                    { ""Id"": ""*"", ""GlobalStat"": 100 }
                ]
            }";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip };
            var fileData = JsonSerializer.Deserialize<FileData<PropertyPatch>>(json, options);

            Assert.NotNull(fileData);
            Assert.Equal(2, fileData.Items.Count);
            Assert.Equal("Hero", fileData.Items[0].ID);
            Assert.Equal("*", fileData.Items[1].ID);

            var heroProps = fileData.Items[0].ExtraProperties;
            Assert.NotNull(heroProps);
            Assert.Contains("Strength", heroProps.Keys);
            Assert.Contains("Magic", heroProps.Keys);

            var starProps = fileData.Items[1].ExtraProperties;
            Assert.NotNull(starProps);
            Assert.Contains("GlobalStat", starProps.Keys);
        }
    }
}
