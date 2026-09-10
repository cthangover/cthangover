using System.Text.Json;
using Cthangover.Core.Items;

namespace Engine.Tests.Items
{
    public class ItemInfoTests
    {
        [Fact]
        public void ItemInfo_ExtraProperties_RoundTrip()
        {
            var json = @"{
                ""Id"": ""test/item"",
                ""Name"": ""Test Item"",
                ""Description"": ""A test item"",
                ""Cost"": 42,
                ""Sprite"": ""test/sprite"",
                ""ItemType"": ""Food"",
                ""ItemAction"": ""use_heal"",
                ""Durability"": 100,
                ""Quality"": ""legendary"",
                ""IsSpecial"": true
            }";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip };
            var info = JsonSerializer.Deserialize<ItemInfo>(json, options);

            Assert.Equal("test/item", info.ID);
            Assert.Equal("Test Item", info.Name);
            Assert.Equal(42, info.Cost);
            Assert.Equal("Food", info.ItemType.ToString());

            Assert.NotNull(info.ExtraProperties);
            Assert.Equal(3, info.ExtraProperties.Count);

            var durability = Assert.IsType<JsonElement>(info.ExtraProperties["Durability"]);
            Assert.Equal(100, durability.GetInt32());

            var quality = Assert.IsType<JsonElement>(info.ExtraProperties["Quality"]);
            Assert.Equal("legendary", quality.GetString());

            var isSpecial = Assert.IsType<JsonElement>(info.ExtraProperties["IsSpecial"]);
            Assert.True(isSpecial.GetBoolean());

            var serializedBack = JsonSerializer.Serialize(info, options);
            var restored = JsonSerializer.Deserialize<ItemInfo>(serializedBack, options);

            Assert.Equal(info.ID, restored.ID);
            Assert.NotNull(restored.ExtraProperties);
            Assert.True(restored.ExtraProperties.ContainsKey("Durability"));
        }

        [Fact]
        public void ItemInfo_NoExtraProperties_IsNull()
        {
            var json = @"{
                ""Id"": ""test/item"",
                ""Name"": ""Test"",
                ""Cost"": 1,
                ""ItemType"": ""Resource""
            }";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip };
            var info = JsonSerializer.Deserialize<ItemInfo>(json, options);

            Assert.Equal("test/item", info.ID);
            Assert.Null(info.ExtraProperties);
        }
    }
}
