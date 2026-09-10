using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Patches;

namespace Engine.Tests.Utils
{
    public class JsonPatchMergerTests
    {
        [Fact]
        public void Merge_NoPatches_ReturnsOriginalJson()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\",\"Value\":42}";

            var result = merger.Merge(json, "Test", null);

            Assert.Contains("\"Id\":\"Test\"", result);
            Assert.Contains("\"Value\":42", result);
        }

        [Fact]
        public void Merge_Wildcard_ScalarOverwrite()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\",\"Health\":50}";

            var patches = Patches("*", ("Health", ParseJson("999")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Health\":999", result);
            Assert.DoesNotContain("\"Health\":50", result);
        }

        [Fact]
        public void Merge_Wildcard_AddsNewKey()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\",\"Health\":50}";

            var patches = Patches("*", ("Strength", ParseJson("10")), ("Magic", ParseJson("5")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Health\":50", result);
            Assert.Contains("\"Strength\":10", result);
            Assert.Contains("\"Magic\":5", result);
        }

        [Fact]
        public void Merge_SpecificOverridesWildcard()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\",\"Value\":10}";

            var patches = new Dictionary<string, List<PropertyPatch>>
            {
                ["*"] = new() { Patch("*", ("Value", ParseJson("100"))) },
                ["Test"] = new() { Patch("Test", ("Value", ParseJson("200"))) }
            };

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Value\":200", result);
        }

        [Fact]
        public void Merge_Array_BareKey_Overwrites()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Skills", "[1, 2]");

            var patches = Patches("Test", ("Skills", ParseJson("[3]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Skills\":[3]", result);
        }

        [Fact]
        public void Merge_Array_Add_Concatenates()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Skills", "[\"slash\",\"block\"]");

            var patches = Patches("Test", ("Skills$add", ParseJson("[\"cook\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"slash\"", result);
            Assert.Contains("\"block\"", result);
            Assert.Contains("\"cook\"", result);
        }

        [Fact]
        public void Merge_Array_Remove_RemovesElements()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Skills", "[\"slash\",\"block\",\"cook\"]");

            var patches = Patches("Test", ("Skills$remove", ParseJson("[\"block\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"slash\"", result);
            Assert.Contains("\"cook\"", result);
            Assert.DoesNotContain("\"block\"", result);
        }

        [Fact]
        public void Merge_Array_Set_Overwrites()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Skills", "[\"slash\",\"block\"]");

            var patches = Patches("Test", ("Skills$set", ParseJson("[\"sword\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Skills\":[\"sword\"]", result);
        }

        [Fact]
        public void Merge_Array_Add_NewKey_Creates()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\"}";

            var patches = Patches("Test", ("Skills$add", ParseJson("[\"cook\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Skills\":[\"cook\"]", result);
        }

        [Fact]
        public void Merge_Array_Remove_NonExistingKey_NoOp()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\"}";

            var patches = Patches("Test", ("Skills$remove", ParseJson("[\"slash\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.DoesNotContain("Skills", result);
        }

        [Fact]
        public void Merge_ExplicitIdentityKey_PerFieldMerge()
        {
            var merger = new JsonPatchMerger();
            var keys = new Dictionary<string, string> { ["Loot"] = "ItemId" };

            var json = JsonArr("Loot",
                "[{\"ItemId\":\"food/bread\",\"MinCount\":1,\"MaxCount\":2,\"Probability\":50}]");

            var patches = Patches("Test",
                ("Loot$add", ParseJson("[{\"ItemId\":\"food/bread\",\"Probability\":80}]")));

            var result = merger.Merge(json, "Test", patches, keys);

            Assert.Contains("\"MinCount\":1", result);
            Assert.Contains("\"MaxCount\":2", result);
            Assert.Contains("\"Probability\":80", result);
            Assert.DoesNotContain("\"Probability\":50", result);
        }

        [Fact]
        public void Merge_ExplicitIdentityKey_RemoveByIdentity()
        {
            var merger = new JsonPatchMerger();
            var keys = new Dictionary<string, string> { ["Loot"] = "ItemId" };

            var json = JsonArr("Loot",
                "[{\"ItemId\":\"food/bread\",\"Probability\":50}," +
                "{\"ItemId\":\"food/meat\",\"Probability\":100}]");

            var patches = Patches("Test",
                ("Loot$remove", ParseJson("[{\"ItemId\":\"food/bread\"}]")));

            var result = merger.Merge(json, "Test", patches, keys);

            Assert.DoesNotContain("food/bread", result);
            Assert.Contains("food/meat", result);
        }

        [Fact]
        public void Merge_AutoDetect_IdField()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Rewards",
                "[{\"Id\":\"gold\",\"Count\":100},{\"Id\":\"exp\",\"Count\":50}]");

            var patches = Patches("Test",
                ("Rewards$add", ParseJson("[{\"Id\":\"gold\",\"Count\":200}]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Count\":200", result);
            Assert.DoesNotContain("\"Count\":100", result);
        }

        [Fact]
        public void Merge_AutoDetect_ItemIdField()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Drops",
                "[{\"ItemId\":\"sword\",\"Chance\":10}]");

            var patches = Patches("Test",
                ("Drops$add", ParseJson("[{\"ItemId\":\"sword\",\"Chance\":20}]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Chance\":20", result);
            Assert.DoesNotContain("\"Chance\":10", result);
        }

        [Fact]
        public void Merge_AutoDetect_RemoveById()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Rewards",
                "[{\"Id\":\"gold\",\"Count\":100},{\"Id\":\"exp\",\"Count\":50}]");

            var patches = Patches("Test",
                ("Rewards$remove", ParseJson("[{\"Id\":\"exp\"}]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Id\":\"gold\"", result);
            Assert.DoesNotContain("\"Id\":\"exp\"", result);
        }

        [Fact]
        public void Merge_AutoDetect_NewElementAppends()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Rewards",
                "[{\"Id\":\"gold\",\"Count\":100}]");

            var patches = Patches("Test",
                ("Rewards$add", ParseJson("[{\"Id\":\"exp\",\"Count\":50}]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("gold", result);
            Assert.Contains("exp", result);
        }

        [Fact]
        public void Merge_StringArray_NoAutoDetect_SimpleConcat()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Tags", "[\"a\",\"b\"]");

            var patches = Patches("Test", ("Tags$add", ParseJson("[\"c\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"a\"", result);
            Assert.Contains("\"b\"", result);
            Assert.Contains("\"c\"", result);
        }

        [Fact]
        public void Merge_Actions_StringList_AddAndRemove()
        {
            var merger = new JsonPatchMerger();
            var json = JsonArr("Actions", "[\"physics/attack\",\"physics/defence\"]");

            var patches = Pad("Test",
                ("Actions$add", ParseJson("[\"magic/heal\"]")),
                ("Actions$remove", ParseJson("[\"physics/defence\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("physics/attack", result);
            Assert.Contains("magic/heal", result);
            Assert.DoesNotContain("physics/defence", result);
        }

        [Fact]
        public void Merge_UnknownSuffixArray_TreatedAsBareKey()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\"}";

            var patches = Patches("Test", ("Skills$unknown", ParseJson("[\"slash\"]")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Skills$unknown\":[\"slash\"]", result);
        }

        [Fact]
        public void Merge_StringPatch_Overwrites()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\",\"Name\":\"old\"}";

            var patches = Patches("Test", ("Name", ParseJson("\"new\"")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Name\":\"new\"", result);
        }

        [Fact]
        public void Merge_BoolPatch_Overwrites()
        {
            var merger = new JsonPatchMerger();
            var json = "{\"Id\":\"Test\",\"Active\":false}";

            var patches = Patches("Test", ("Active", ParseJson("true")));

            var result = merger.Merge(json, "Test", patches);

            Assert.Contains("\"Active\":true", result);
        }

        [Fact]
        public void DetectIdentityKey_ObjectWithId_ReturnsId()
        {
            var arr = ParseJson("[{\"Id\":\"gold\",\"Count\":100}]");
            var key = JsonPatchMerger.DetectIdentityKey(arr);
            Assert.Equal("Id", key);
        }

        [Fact]
        public void DetectIdentityKey_ObjectWithItemId_ReturnsItemId()
        {
            var arr = ParseJson("[{\"ItemId\":\"sword\",\"Chance\":10}]");
            var key = JsonPatchMerger.DetectIdentityKey(arr);
            Assert.Equal("ItemId", key);
        }

        [Fact]
        public void DetectIdentityKey_ObjectWithQuestId_ReturnsQuestId()
        {
            var arr = ParseJson("[{\"QuestId\":\"q1\",\"Name\":\"test\"}]");
            var key = JsonPatchMerger.DetectIdentityKey(arr);
            Assert.Equal("QuestId", key);
        }

        [Fact]
        public void DetectIdentityKey_ObjectWithoutIdField_ReturnsNull()
        {
            var arr = ParseJson("[{\"Name\":\"test\",\"Count\":1}]");
            var key = JsonPatchMerger.DetectIdentityKey(arr);
            Assert.Null(key);
        }

        [Fact]
        public void DetectIdentityKey_StringArray_ReturnsNull()
        {
            var arr = ParseJson("[\"a\",\"b\"]");
            var key = JsonPatchMerger.DetectIdentityKey(arr);
            Assert.Null(key);
        }

        [Fact]
        public void DetectIdentityKey_EmptyArray_ReturnsNull()
        {
            var arr = ParseJson("[]");
            var key = JsonPatchMerger.DetectIdentityKey(arr);
            Assert.Null(key);
        }

        [Fact]
        public void BuildMergeKeys_WithIMergeable_ReturnsMapping()
        {
            var keys = JsonPatchMerger.BuildMergeKeys<TestMergeEntity>();
            Assert.True(keys.ContainsKey("Loot"));
            Assert.Equal("ItemId", keys["Loot"]);
            Assert.False(keys.ContainsKey("SimpleList"));
        }

        private class TestMergeEntity
        {
            public List<TestMergeable> Loot { get; set; }
            public List<string> SimpleList { get; set; }
        }

        private class TestMergeable : IMergeable
        {
            public string MergeKeyProperty => "ItemId";
            public string ItemId { get; set; }
        }

        private static JsonElement ParseJson(string json)
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        private static Dictionary<string, List<PropertyPatch>> Patches(
            string targetId, params (string key, JsonElement value)[] props)
        {
            return new Dictionary<string, List<PropertyPatch>>
            {
                [targetId] = new List<PropertyPatch> { Patch(targetId, props) }
            };
        }

        private static Dictionary<string, List<PropertyPatch>> Pad(
            string targetId, params (string key, JsonElement value)[] props)
        {
            var patch = new PropertyPatch
            {
                ID = targetId,
                ExtraProperties = new Dictionary<string, object>()
            };
            foreach (var (key, value) in props)
                patch.ExtraProperties[key] = value;

            return new Dictionary<string, List<PropertyPatch>>
            {
                [targetId] = new List<PropertyPatch> { patch }
            };
        }

        private static PropertyPatch Patch(
            string id, params (string key, JsonElement value)[] props)
        {
            var patch = new PropertyPatch
            {
                ID = id,
                ExtraProperties = new Dictionary<string, object>()
            };
            foreach (var (key, value) in props)
                patch.ExtraProperties[key] = value;

            return patch;
        }

        private static string JsonArr(string key, string arrJson)
        {
            return "{\"Id\":\"Test\"," + "\"" + key + "\":" + arrJson + "}";
        }
    }
}
