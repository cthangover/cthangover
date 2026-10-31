namespace Engine.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Patches;
    using Engine.Tests.Utils.PatchSamples;

    /// <summary>
    /// Patch engine v2. The P-numbers name the cases of the 2026-10-30 audit
    /// (<c>claude_check.md</c> section 1) that the old text-level merger got wrong.
    /// </summary>
    public class JsonPatchMergerTests
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        // --- P1: no identity guessed from property names ---------------------------------------

        [Fact]
        public void Add_ObjectsWithoutDeclaredKey_AppendEvenWhenAPropertyEndsInId()
        {
            var result = Patch("""{"Pages":[{"Condition":"wolf == 1","Solid":true,"Route":"loop"}]}""",
                """{"Pages$add":[{"Condition":"probe == 1","Solid":false}]}""");

            var pages = result["Pages"].AsArray();
            Assert.Equal(2, pages.Count);
            Assert.Equal("wolf == 1", pages[0]["Condition"].GetValue<string>());
            Assert.Equal("loop", pages[0]["Route"].GetValue<string>());
            Assert.Equal("probe == 1", pages[1]["Condition"].GetValue<string>());
        }

        [Fact]
        public void Add_ElementsWithIdButNoDeclaredKey_Append()
        {
            var result = Patch("""{"Rewards":[{"Id":"gold","Count":100}]}""",
                """{"Rewards$add":[{"Id":"gold","Count":200}]}""");

            Assert.Equal(2, result["Rewards"].AsArray().Count);
        }

        // --- P3–P6: nested objects merge recursively, operations at any depth ---------------------

        [Fact]
        public void Merge_PartialNestedObject_KeepsSiblingFields()
        {
            var result = Patch("""{"Spawn":{"Map":"village","X":1,"Y":4,"Dir":"Up"}}""", """{"Spawn":{"X":16}}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Map":"village","X":16,"Y":4,"Dir":"Up"}"""), result["Spawn"]));
        }

        [Fact]
        public void Add_InsideNestedObject_AppendsAndLeavesNoOperationKey()
        {
            var result = Patch("""{"Encounters":{"Steps":20,"Groups":["slime"]}}""",
                """{"Encounters":{"Groups$add":["wolf"]}}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Steps":20,"Groups":["slime","wolf"]}"""), result["Encounters"]));
        }

        [Fact]
        public void Add_ByIMergeableKey_DeepMergesNestedObjectOfTheElement()
        {
            var tileset = new PatchTilesetSample
            {
                Id = "village",
                Tiles = new List<PatchTileSample>
                {
                    new() { Index = 29, Tag = "lamp", Light = new PatchLightSample { Radius = 2, Color = "#ffcf80" } },
                },
            };

            var patched = PatchTyped(tileset, """{"Tiles$add":[{"Index":29,"Light":{"Radius":6}}]}""");

            var tile = Assert.Single(patched.Tiles);
            Assert.Equal("lamp", tile.Tag);
            Assert.Equal(6, tile.Light.Radius);
            Assert.Equal("#ffcf80", tile.Light.Color);
        }

        [Fact]
        public void Merge_OneArrayOfNestedObject_KeepsTheOtherArrays()
        {
            var result = Patch("""{"Layers":{"Back":["0 0"],"Main":[". 5"],"Front":[". ."]}}""",
                """{"Layers":{"Front":["7 ."]}}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Back":["0 0"],"Main":[". 5"],"Front":["7 ."]}"""), result["Layers"]));
        }

        [Fact]
        public void Merge_ObjectIntoMissingProperty_CreatesItAndResolvesItsOperations()
        {
            var result = Patch("""{"Id":"a"}""", """{"Encounters":{"Steps":5,"Groups$add":["wolf"],"Gone":null}}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Steps":5,"Groups":["wolf"]}"""), result["Encounters"]));
        }

        [Fact]
        public void Add_KeyDeclaredOnANestedType_MergesAtThatDepth()
        {
            var sample = new PatchObjectSample
            {
                Id = "door",
                States = new List<PatchStateSample>
                {
                    new() { Id = "open", When = "self.open == 1", Parts = new List<PatchPartSample> { new() { Id = "leaf", Image = "door_open", X = 2 } } },
                },
            };

            var patched = PatchTyped(sample, """{"States$add":[{"Id":"OPEN","Parts$add":[{"Id":"leaf","Image":"door_open_red"}]}]}""");

            var state = Assert.Single(patched.States);
            Assert.Equal("self.open == 1", state.When);
            var part = Assert.Single(state.Parts);
            Assert.Equal("door_open_red", part.Image);
            Assert.Equal(2, part.X);
        }

        [Fact]
        public void Merge_DictionaryValues_UseTheValueTypeKeys()
        {
            var sample = new PatchObjectSample
            {
                Id = "oak",
                Variants = new Dictionary<string, PatchObjectSample>
                {
                    ["winter"] = new() { Parts = new List<PatchPartSample> { new() { Id = "crown", Image = "oak_snow", X = 1 } } },
                },
            };

            var patched = PatchTyped(sample, """{"Variants":{"winter":{"Parts$add":[{"Id":"crown","Image":"oak_ice"}]}}}""");

            var part = Assert.Single(patched.Variants["winter"].Parts);
            Assert.Equal("oak_ice", part.Image);
            Assert.Equal(1, part.X);
        }

        // --- P7, P8: $remove compares values, not text -------------------------------------------

        [Fact]
        public void Remove_StringsWithQuotesCyrillicAndOperators_MatchTheSerialisedEntity()
        {
            var commands = new List<string> { "ptext \"Привет!\" key=rpg/merchant/hello", "flag a 1", "when \"x && item('money/coin') > 0\" -> :buy" };
            var entity = JsonSerializer.Serialize(new { Commands = commands });

            var result = JsonPatchMerger.ParseObject(entity);
            JsonPatchMerger.Apply(result, Obj("""{"Commands$remove":["ptext \"Привет!\" key=rpg/merchant/hello","when \"x && item('money/coin') > 0\" -> :buy"]}"""), null);

            Assert.Equal(new[] { "flag a 1" }, result["Commands"].AsArray().Select(n => n.GetValue<string>()));
        }

        [Fact]
        public void Remove_WholeObjectWithoutKey_IgnoresNullFieldsOfTheEntity()
        {
            var result = Patch("""{"DrawOrderOverrides":[{"Layer":"Main","X":1,"Y":0,"Order":"Above","Extra":null},{"Layer":"Main","X":2,"Y":0,"Order":"Below"}]}""",
                """{"DrawOrderOverrides$remove":[{"layer":"Main","X":1.0,"Y":0,"Order":"Above"}]}""");

            var left = Assert.Single(result["DrawOrderOverrides"].AsArray());
            Assert.Equal(2, left["X"].GetValue<int>());
        }

        [Fact]
        public void Remove_ExplicitKeyForm_RemovesByThatProperty()
        {
            var result = Patch("""{"Pages":[{"Condition":"wolf == 1","Solid":false},{"Condition":"night == 1"}]}""",
                """{"Pages$remove":{"$key":"Condition","$items":[{"Condition":"wolf == 1"}]}}""");

            var left = Assert.Single(result["Pages"].AsArray());
            Assert.Equal("night == 1", left["Condition"].GetValue<string>());
        }

        [Fact]
        public void Remove_ByDeclaredKey_AcceptsTheBareKeyValue()
        {
            var sample = new PatchObjectSample
            {
                Id = "house",
                Parts = new List<PatchPartSample> { new() { Id = "roof" }, new() { Id = "walls" } },
            };

            var patched = PatchTyped(sample, """{"Parts$remove":["Roof"]}""");

            Assert.Equal("walls", Assert.Single(patched.Parts).Id);
        }

        // --- P9: composite keys -----------------------------------------------------------------

        [Fact]
        public void Add_CompositeMergeKey_ReplacesTheSameCell()
        {
            var sample = new PatchObjectSample
            {
                Id = "map",
                Cells = new List<PatchCellSample> { new() { Layer = "Main", X = 1, Y = 0, Order = "Above" } },
            };

            var patched = PatchTyped(sample, """{"Overrides$add":[{"Layer":"Main","X":1,"Y":0,"Order":"Below"},{"Layer":"Main","X":1,"Y":1,"Order":"Above"}]}""");

            Assert.Equal(2, patched.Cells.Count);
            Assert.Equal("Below", patched.Cells[0].Order);
        }

        [Fact]
        public void Add_ExplicitCompositeKey_ReplacesTheSameCell()
        {
            var result = Patch("""{"Cells":[{"Layer":"Main","X":1,"Y":0,"Order":"Above"}]}""",
                """{"Cells$add":{"$key":["Layer","X","Y"],"$items":[{"Layer":"Main","X":1,"Y":0,"Order":"Below"}]}}""");

            var cell = Assert.Single(result["Cells"].AsArray());
            Assert.Equal("Below", cell["Order"].GetValue<string>());
        }

        // --- replacement, null, $set ------------------------------------------------------------

        [Fact]
        public void Merge_ScalarAndArrayWithoutOperation_Replace()
        {
            var result = Patch("""{"Name":"old","Active":false,"Skills":[1,2]}""", """{"Name":"new","Active":true,"Skills":[3]}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Name":"new","Active":true,"Skills":[3]}"""), result));
        }

        [Fact]
        public void Merge_Null_RemovesTheProperty()
        {
            var result = Patch("""{"Teleport":{"Map":"house"},"Name":"door"}""", """{"teleport":null}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Name":"door"}"""), result));
            Assert.Single(result);
        }

        [Fact]
        public void Set_Object_ReplacesWithoutMerging()
        {
            var result = Patch("""{"Spawn":{"Map":"village","X":1,"Y":4}}""", """{"Spawn$set":{"X":16}}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"X":16}"""), result["Spawn"]));
        }

        [Fact]
        public void Merge_KeyInOtherCase_ReplacesTheEntityPropertyKeepingItsName()
        {
            var result = Patch("""{"Health":50,"Tags":["a"]}""", """{"health":999,"tags$add":["b"]}""");

            Assert.Equal(new[] { "Health", "Tags" }, result.Select(p => p.Key));
            Assert.Equal(999, result["Health"].GetValue<int>());
            Assert.Equal(new[] { "a", "b" }, result["Tags"].AsArray().Select(n => n.GetValue<string>()));
        }

        [Fact]
        public void Merge_ReplacementThenAddInOnePatch_AddsToTheReplacement()
        {
            var result = Patch("""{"Tiles":[1]}""", """{"Tiles":[2],"tiles$add":[3]}""");

            Assert.Equal(new[] { 2, 3 }, result["Tiles"].AsArray().Select(n => n.GetValue<int>()));
        }

        [Fact]
        public void Add_ToMissingArray_CreatesIt_RemoveFromMissing_IsNoOp()
        {
            var result = Patch("""{"Id":"t"}""", """{"Skills$add":["cook"],"Spells$remove":["fire"]}""");

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Id":"t","Skills":["cook"]}"""), result));
        }

        [Fact]
        public void Add_WithoutKey_DoesNotDeduplicate()
        {
            var result = Patch("""{"Actions":["attack"]}""", """{"Actions$add":["attack","stun"]}""");

            Assert.Equal(new[] { "attack", "attack", "stun" }, result["Actions"].AsArray().Select(n => n.GetValue<string>()));
        }

        // --- errors -----------------------------------------------------------------------------

        [Fact]
        public void Add_OnAScalar_ThrowsWithThePath()
        {
            var error = Assert.Throws<JsonPatchException>(() =>
                Patch("""{"Page":{"Condition":"x"}}""", """{"Page":{"Condition$add":["y"]}}"""));

            Assert.Equal("Page.Condition$add", error.Path);
            Assert.Contains("not an array", error.Message);
        }

        [Fact]
        public void UnknownOperation_Throws()
        {
            var error = Assert.Throws<JsonPatchException>(() => Patch("""{"Skills":[]}""", """{"Skills$append":["x"]}"""));

            Assert.Equal("Skills$append", error.Path);
        }

        [Fact]
        public void Add_ValueNeitherArrayNorExplicitForm_Throws()
        {
            Assert.Throws<JsonPatchException>(() => Patch("""{"Skills":[]}""", """{"Skills$add":"x"}"""));
            Assert.Throws<JsonPatchException>(() => Patch("""{"Skills":[]}""", """{"Skills$add":{"$key":"Id"}}"""));
            Assert.Throws<JsonPatchException>(() => Patch("""{"Skills":[]}""", """{"Skills$add":{"$key":"Id","$items":[],"Other":1}}"""));
        }

        [Fact]
        public void Remove_KeyedElementWithoutTheKey_Throws()
        {
            Assert.Throws<JsonPatchException>(() =>
                Patch("""{"Cells":[{"Layer":"Main","X":1,"Y":0}]}""", """{"Cells$remove":{"$key":["Layer","X","Y"],"$items":[{"Layer":"Main"}]}}"""));
        }

        // --- Merge: wildcard then addressed patches ----------------------------------------------

        [Fact]
        public void Merge_AppliesWildcardThenAddressedPatches_InListOrder()
        {
            var patches = new Dictionary<string, List<PropertyPatch>>
            {
                ["Test"] = new() { Prop("Test", ("Value", "200")), Prop("Test", ("Tags$add", "[\"b\"]")) },
                ["*"] = new() { Prop("*", ("Value", "100"), ("Global", "1")) },
            };

            var result = JsonNode.Parse(JsonPatchMerger.Merge("""{"Id":"Test","Value":10,"Tags":["a"]}""", "Test", patches));

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"Id":"Test","Value":200,"Tags":["a","b"],"Global":1}"""), result));
        }

        [Fact]
        public void Merge_NoPatches_ReturnsTheInput()
        {
            const string json = """{"Id":"Test","Value":42}""";

            Assert.Same(json, JsonPatchMerger.Merge(json, "Test", null));
            Assert.Same(json, JsonPatchMerger.Merge(json, "Test", new Dictionary<string, List<PropertyPatch>>()));
        }

        [Fact]
        public void ToNode_TakesEveryOperationOfThePatch()
        {
            var file = JsonSerializer.Deserialize<FileData<PropertyPatch>>("""{"Items":[{"Id":"x","A":1,"B$add":[2],"C":null}]}""", ReadOptions);

            var node = JsonPatchMerger.ToNode(file.Items[0]);

            Assert.True(JsonSemantic.Equal(JsonNode.Parse("""{"A":1,"B$add":[2]}"""), node));
            Assert.Contains(node, p => p.Key == "C" && p.Value == null);
        }

        [Fact]
        public void ParseObject_AllowsComments_RejectsNonObjects()
        {
            Assert.Equal(1, JsonPatchMerger.ParseObject("{ /* c */ \"A\": 1 }")["A"].GetValue<int>());
            Assert.Throws<JsonException>(() => JsonPatchMerger.ParseObject("[1]"));
        }

        private static JsonObject Patch(string target, string patch)
        {
            var result = JsonPatchMerger.ParseObject(target);
            JsonPatchMerger.Apply(result, Obj(patch), null);
            return result;
        }

        private static T PatchTyped<T>(T value, string patch)
        {
            var node = JsonPatchMerger.ParseObject(JsonSerializer.Serialize(value, ReadOptions));
            JsonPatchMerger.Apply(node, Obj(patch), typeof(T));
            return JsonSerializer.Deserialize<T>(node.ToJsonString(), ReadOptions);
        }

        private static JsonObject Obj(string json) => JsonNode.Parse(json).AsObject();

        private static PropertyPatch Prop(string id, params (string key, string json)[] props)
        {
            var patch = new PropertyPatch { ID = id, ExtraProperties = new Dictionary<string, object>() };
            foreach (var (key, json) in props)
                patch.ExtraProperties[key] = JsonSerializer.Deserialize<JsonElement>(json);
            return patch;
        }
    }
}
