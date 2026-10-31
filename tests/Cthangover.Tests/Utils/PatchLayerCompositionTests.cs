namespace Engine.Tests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;
    using Engine.Tests.Utils.PatchSamples;

    /// <summary>
    /// Patches written into stored layers (<see cref="PatchLayerAttribute"/>, plan item A2b): a patch of
    /// a variant, a placement's per-instance fields or a state's components composes with the layer
    /// instead of running its operations on it. The law for merge layers: applying the composed layer to
    /// any object equals applying the old layer, then the patch.
    /// </summary>
    public class PatchLayerCompositionTests
    {
        private static readonly Type Object = typeof(PatchLayeredObjectSample);

        /// <summary>Objects the layers are applied to: full, empty, and with other parts and a teleport.</summary>
        private static readonly string[] Bases =
        {
            """{"Id":"o","Name":"n","Parts":[{"Id":"a","Image":"a.png","X":1},{"Id":"b","Image":"b.png"}],"Commands":["say 1","c1"],"Interact":{"Scenario":"s","Commands":["c1"]}}""",
            """{"Id":"o"}""",
            """{"Id":"o","Parts":[{"Id":"c","Image":"c.png"}],"Interact":{"Teleport":"t"}}""",
        };

        /// <summary>(stored layer, patch written into it) for every composition rule.</summary>
        public static IEnumerable<object[]> Cases()
        {
            // K, K: null, K$set replace whatever the layer did to K.
            yield return Case("""{"Name":"x","Parts$add":[{"Id":"a","X":4}]}""", """{"Name":"y"}""");
            yield return Case("""{"Parts$add":[{"Id":"a","X":4}],"Parts$remove":["b"]}""", """{"Parts":[{"Id":"z","Image":"z.png"}]}""");
            yield return Case("""{"Parts$add":[{"Id":"a","X":4}]}""", """{"Parts":null}""");
            yield return Case("""{"Interact":{"Commands$add":["c2"]}}""", """{"Interact$set":{"Scenario":"q"}}""");
            yield return Case("""{"Commands$add":["x"]}""", """{"Commands$set":["only"]}""");
            // An object composes with a layer object, merges into a concrete value, replaces a list.
            yield return Case("""{"Interact":{"Commands$add":["c2"]}}""", """{"Interact":{"Scenario":"z","Commands$add":["c3"]}}""");
            yield return Case("""{"Interact":{"Commands":["c2"],"Teleport":null}}""", """{"Interact":{"Commands$add":["c3"],"Scenario":null}}""");
            yield return Case("""{"Interact":null}""", """{"Interact":{"Scenario":"z","Commands$add":["c3"]}}""");
            yield return Case("""{"Interact$set":{"Scenario":"q","Commands":["k"]}}""", """{"Interact":{"Commands$add":["c"]}}""");
            yield return Case("""{"Commands$add":["x"]}""", """{"Commands":{"Bad":1}}""");
            // An operation on a concrete value runs on it.
            yield return Case("""{"Commands":["a"]}""", """{"Commands$add":["b"]}""");
            yield return Case("""{"Parts$set":[{"Id":"a"},{"Id":"b"}]}""", """{"Parts$add":[{"Id":"a","X":3}],"Parts$remove":["b"]}""");
            yield return Case("""{"Commands":null}""", """{"Commands$remove":["c1"],"Commands$add":["new"]}""");
            // Additions: by key they compose with the layer's addition of that key, otherwise append.
            yield return Case("""{"Parts$add":[{"Id":"a","X":5}]}""", """{"Parts$add":[{"Id":"A","Image":"z.png"},{"Id":"n","Image":"n.png"}]}""");
            yield return Case("""{"Commands$add":["x"]}""", """{"Commands$add":["y","x"]}""");
            yield return Case("""{"Parts$remove":["b"]}""", """{"Parts$add":[{"Id":"b","Image":"again.png"}]}""");
            // Removals unite with the layer's and strike its matching additions.
            yield return Case("""{"Parts$remove":["b"],"Parts$add":[{"Id":"a","X":9},{"Id":"n"}]}""", """{"Parts$remove":["n","a"]}""");
            yield return Case("""{"Commands$add":["x","y","x"]}""", """{"Commands$remove":["x","c1"]}""");
            yield return Case("""{"Parts$add":[{"Id":"n","Image":"n.png"}]}""", """{"Parts$remove":[{"Id":"n"}],"Parts$add":[{"Id":"n","X":2}]}""");
            // A layer written add-before-remove: the same element added back by the patch.
            yield return Case("""{"Parts$add":[{"Id":"a","X":2},{"Id":"m"}],"Parts$remove":["a"]}""", """{"Parts$add":[{"Id":"a","Image":"q.png"}]}""");
            yield return Case("""{"Commands$add":["x"],"Commands$remove":["x"]}""", """{"Commands$add":["x"]}""");
            // The explicit key form keeps its key.
            yield return Case("""{"Parts$add":{"$key":"Image","$items":[{"Image":"a.png","X":7}]}}""",
                """{"Parts$add":{"$key":"Image","$items":[{"Image":"a.png","Id":"zz"},{"Image":"new.png"}]}}""");
            // Keyed lists inside keyed elements of the layer (states of a variant) compose as fragments.
            yield return Case("""{"Parts$add":[{"Id":"a","Image":"w.png"}],"Name":"winter"}""", """{"Parts$add":[{"Id":"a","X":-1}],"Name$set":"snow"}""");
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Variant_PatchedThenApplied_EqualsLayerThenPatch(string layer, string patch)
        {
            var owner = Parse("""{"Id":"o","Variants":{"winter":""" + layer + "}}");
            JsonPatchMerger.Apply(owner, Parse("""{"Variants":{"Winter":""" + patch + "}}"), Object);
            var composed = owner["Variants"]["winter"].AsObject();

            foreach (var x in Bases)
                AssertSameEffect(x, layer, patch, composed);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void PlacementOverrides_PatchedThenApplied_EqualLayerThenPatch(string layer, string patch)
        {
            var placement = Parse("""{"Id":"map/p1","Object":"o","X":3,"Y":4}""");
            foreach (var pair in Parse(layer))
                placement[pair.Key] = pair.Value?.DeepClone();
            JsonPatchMerger.Apply(placement, Parse(patch), typeof(PatchPlacementSample));

            Assert.Equal("o", placement["Object"].GetValue<string>());
            Assert.Equal(3, placement["X"].GetValue<int>());
            var composed = new JsonObject(placement.Where(p => p.Key is not ("Id" or "Object" or "X" or "Y"))
                .Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
            foreach (var x in Bases)
                AssertSameEffect(x, layer, patch, composed);
        }

        [Fact]
        public void Variant_NewEntry_KeepsOperationsForLater()
        {
            var owner = Parse("""{"Id":"o","Parts":[{"Id":"roof","Image":"r.png"}]}""");
            JsonPatchMerger.Apply(owner, Parse("""{"Variants":{"winter":{"Parts$add":[{"Id":"snow","Image":"s.png"}]}}}"""), Object);

            var winter = owner["Variants"]["winter"].AsObject();
            Assert.Null(Find(winter, "Parts"));
            Assert.Single(winter["Parts$add"].AsArray());
            var applied = Apply(owner.ToJsonString(), winter);
            Assert.Equal(new[] { "roof", "snow" }, applied["Parts"].AsArray().Select(p => p["Id"].GetValue<string>()));
        }

        [Fact]
        public void Placement_CommandsAddWithoutInteract_AddsToTheObjectsCommands()
        {
            // The request from stream R: before A2b this created Interact.Commands that replaced the object's.
            var placement = Parse("""{"Id":"rpg_village/merchant","Object":"npc","X":1,"Y":2}""");
            JsonPatchMerger.Apply(placement, Parse("""{"Interact":{"Commands$add":["flag self.met 1"]}}"""), typeof(PatchPlacementSample));

            var layer = new JsonObject { ["Interact"] = placement["Interact"].DeepClone() };
            var effective = Apply("""{"Id":"npc","Interact":{"Scenario":"talk","Commands":["say hi"]}}""", layer);
            Assert.Equal("talk", effective["Interact"]["Scenario"].GetValue<string>());
            Assert.Equal(new[] { "say hi", "flag self.met 1" }, effective["Interact"]["Commands"].AsArray().Select(c => c.GetValue<string>()));
        }

        [Fact]
        public void Variant_ListOperationOnAnObjectTheLayerMerges_Throws()
        {
            var owner = Parse("""{"Id":"o","Variants":{"winter":{"Interact":{"Scenario":"q"}}}}""");

            var error = Assert.Throws<JsonPatchException>(() =>
                JsonPatchMerger.Apply(owner, Parse("""{"Variants":{"winter":{"Interact$add":["x"]}}}"""), Object));
            Assert.Equal("Variants.winter.Interact$add", error.Path);
        }

        [Fact]
        public void Variant_DifferentExplicitKeys_Throws()
        {
            var owner = Parse("""{"Variants":{"winter":{"Parts$add":{"$key":"Image","$items":[{"Image":"a"}]}}}}""");

            Assert.Throws<JsonPatchException>(() => JsonPatchMerger.Apply(owner,
                Parse("""{"Variants":{"winter":{"Parts$remove":{"$key":"Id","$items":["a"]}}}}"""), Object));
        }

        [Fact]
        public void Variant_SetOrNullAtDictionaryLevel_ReplacesTheEntryAsWritten()
        {
            var owner = Parse("""{"Variants":{"winter":{"Name":"w"},"autumn":{"Name":"a"}}}""");
            JsonPatchMerger.Apply(owner, Parse("""{"Variants":{"winter$set":{"Parts$add":[{"Id":"x"}]},"autumn":null}}"""), Object);

            Assert.True(Canonical(Parse("""{"winter":{"Parts$add":[{"Id":"x"}]}}""")) == Canonical(owner["Variants"]));
        }

        [Fact]
        public void State_PatchOfANamedComponent_ChangesItAsData()
        {
            var obj = Parse("""{"Id":"door","States":[{"Id":"locked","When":"!flag(key)","Interact":{"Scenario":"locked","Commands":["a"]}}]}""");
            JsonPatchMerger.Apply(obj, Parse("""{"States$add":[{"Id":"locked","Interact":{"Commands$add":["b"]}}]}"""), Object);

            var interact = obj["States"][0]["Interact"];
            Assert.Equal("locked", interact["Scenario"].GetValue<string>());
            Assert.Equal(new[] { "a", "b" }, interact["Commands"].AsArray().Select(c => c.GetValue<string>()));
        }

        [Fact]
        public void State_WholeNewComponentAndExplicitNull_AreStoredAsWritten()
        {
            var obj = Parse("""{"Id":"door","States":[{"Id":"open","When":"self.open","Parts":[{"Id":"leaf"}]}]}""");
            JsonPatchMerger.Apply(obj, Parse("""{"States$add":[{"Id":"open","Interact":{"Teleport":"house"},"Parts$set":null}]}"""), Object);

            var state = obj["States"][0].AsObject();
            Assert.Equal("house", state["Interact"]["Teleport"].GetValue<string>());
            Assert.True(state.ContainsKey("Parts"));
            Assert.Null(state["Parts"]);
        }

        [Theory]
        [InlineData("""{"States$add":[{"Id":"open","Interact":{"Commands$add":["x"]}}]}""", "States$add[Id=\"open\"].Interact")]
        [InlineData("""{"States$add":[{"Id":"open","Parts$add":[{"Id":"x"}]}]}""", "States$add[Id=\"open\"].Parts$add")]
        [InlineData("""{"States$add":[{"Id":"new","When":"x","Parts$remove":["a"]}]}""", "States$add[1].Parts$remove")]
        public void State_OperationOnAComponentItDoesNotName_Throws(string patch, string path)
        {
            var obj = Parse("""{"Id":"door","States":[{"Id":"open","When":"self.open","Solid":false}]}""");

            var error = Assert.Throws<JsonPatchException>(() => JsonPatchMerger.Apply(obj, Parse(patch), Object));
            Assert.Equal(path, error.Path);
        }

        [Fact]
        public void LeafPaths_InsideLayers_OperationsAreOpaqueLeaves()
        {
            var obj = Parse("""{"Id":"o","Commands$add":["skipped"],"Variants":{"winter":{"Parts$add":[{"Id":"s"}],"Name":"w"}}}""");
            var leaves = JsonLeafPaths.Collect(obj, Object);

            Assert.False(leaves.ContainsKey("Commands$add"));
            Assert.Equal("""[{"Id":"s"}]""", leaves["Variants.winter.Parts$add"].ToJsonString());
            Assert.Equal("w", leaves["Variants.winter.Name"].GetValue<string>());

            var placement = Parse("""{"Id":"m/p","Object":"o","X":1,"Interact":{"Commands$add":["c"]},"Parts":[{"Id":"a","X":2}],"Light":null}""");
            var placementLeaves = JsonLeafPaths.Collect(placement, typeof(PatchPlacementSample));
            Assert.Equal("""["c"]""", placementLeaves["Interact.Commands$add"].ToJsonString());
            Assert.Equal(2, placementLeaves["Parts[Id=\"a\"].X"].GetValue<int>());
            Assert.True(placementLeaves.ContainsKey("Light"));
        }

        [Theory]
        [InlineData("""{"winter":{"Parts$add":[{"Id":"a","X":1}],"Name":"w"}}""", """{"winter":{"Parts$add":[{"Id":"a","X":2}],"Name":"w"}}""")]
        [InlineData("""{"winter":{"Parts$add":[{"Id":"a"},{"Id":"b"}]}}""", """{"winter":{"Parts$add":[{"Id":"a"}]}}""")]
        [InlineData("""{"winter":{"Interact":null,"Name":"w"}}""", """{"winter":{"Name":"w"}}""")]
        [InlineData("""{"winter":{"Name":"w"}}""", """{"winter":{"Name":"w","Interact":null}}""")]
        [InlineData("""{"winter":{"Name":"w"}}""", """{"winter":{"Name":"w"},"spring":{"Commands$add":["x"],"Commands$remove":["y"]}}""")]
        [InlineData("""{"winter":{"Name":"w"},"autumn":{}}""", """{"autumn":{"Interact":{"Commands$add":["x"]}}}""")]
        public void Diff_OfVariants_ReproducesTheEditedLayersExactly(string before, string after)
        {
            var baseObject = Parse("""{"Id":"o","Variants":""" + before + "}");
            var edited = Parse("""{"Id":"o","Variants":""" + after + "}");

            var patch = JsonPatchDiff.Diff(baseObject, edited, Object);
            JsonPatchMerger.Apply(baseObject, patch, Object);

            Assert.Equal(Canonical(edited), Canonical(baseObject));
        }

        [Theory]
        [InlineData("""{"Interact":{"Commands$add":["a"]}}""", """{"Interact":{"Commands$add":["a","b"]}}""")]
        [InlineData("""{"Name":"x"}""", """{"Name":"x","Interact":null}""")]
        [InlineData("""{"Parts$add":[{"Id":"a","X":1}]}""", """{"Parts$add":[{"Id":"a","X":1,"Image":"i"}]}""")]
        [InlineData("""{}""", """{"Light":{"Radius":2}}""")]
        public void Diff_OfPlacementOverrides_Reproduces(string before, string after)
        {
            var baseObject = Placement(before);
            var edited = Placement(after);

            var patch = JsonPatchDiff.Diff(baseObject, edited, typeof(PatchPlacementSample));
            JsonPatchMerger.Apply(baseObject, patch, typeof(PatchPlacementSample));

            Assert.Equal(Canonical(edited), Canonical(baseObject));
        }

        [Fact]
        public void Diff_OfPlacementOverrideDropped_ThrowsBecauseAPatchCannotExpressIt()
        {
            var error = Assert.Throws<JsonPatchException>(() =>
                JsonPatchDiff.Diff(Placement("""{"Interact":{"Scenario":"s"},"Name":"x"}"""), Placement("""{"Name":"x"}"""), typeof(PatchPlacementSample)));
            Assert.Equal("Interact", error.Path);
        }

        [Theory]
        [InlineData("""{"Id":"s","When":"w","Interact":{"Scenario":"a"}}""", """{"Id":"s","When":"w","Interact":null}""")]
        [InlineData("""{"Id":"s","When":"w","Interact":null}""", """{"Id":"s","When":"w"}""")]
        [InlineData("""{"Id":"s","When":"w","Interact":{"Scenario":"a","Commands":["x"]}}""", """{"Id":"s","When":"w","Interact":{"Scenario":"a","Commands":["x","y"]},"Solid":true}""")]
        public void Diff_OfStateComponents_KeepsNullApartFromMissing(string before, string after)
        {
            var baseObject = Parse("""{"Id":"o","States":[""" + before + "]}");
            var edited = Parse("""{"Id":"o","States":[""" + after + "]}");

            var patch = JsonPatchDiff.Diff(baseObject, edited, Object);
            JsonPatchMerger.Apply(baseObject, patch, Object);

            Assert.Equal(Canonical(edited), Canonical(baseObject));
        }

        [Fact]
        public void LayeredSamples_JsonRoundTrip_KeepNullsAndOperations()
        {
            const string json = """{"Id":"m/p","Object":"o","X":1,"Y":2,"Interact":null,"Parts$add":[{"Id":"a"}]}""";
            var placement = JsonSerializer.Deserialize<PatchPlacementSample>(json);
            var restored = JsonSerializer.Deserialize<PatchPlacementSample>(JsonSerializer.Serialize(placement));

            Assert.Equal("m/p", restored.Id);
            Assert.Equal(JsonValueKind.Null, restored.Overrides["Interact"].ValueKind);
            Assert.Equal(JsonValueKind.Array, restored.Overrides["Parts$add"].ValueKind);
        }

        private static void AssertSameEffect(string x, string layer, string patch, JsonObject composed)
        {
            JsonObject expected;
            try
            {
                expected = Apply(x, Parse(layer));
                JsonPatchMerger.Apply(expected, Parse(patch), Object);
            }
            catch (JsonPatchException)
            {
                Assert.Throws<JsonPatchException>(() => Apply(x, composed));
                return;
            }
            var actual = Apply(x, composed);
            Assert.True(JsonSemantic.Equal(expected, actual),
                $"base {x}\nlayer {layer}\npatch {patch}\ncomposed {composed.ToJsonString()}\nexpected {expected.ToJsonString()}\nactual {actual.ToJsonString()}");
        }

        private static JsonObject Apply(string x, JsonObject layer)
        {
            var target = Parse(x);
            JsonPatchMerger.Apply(target, (JsonObject)layer.DeepClone(), Object);
            return target;
        }

        private static JsonObject Placement(string overrides)
        {
            var placement = Parse("""{"Id":"m/p","Object":"o","X":1,"Y":2}""");
            foreach (var pair in Parse(overrides))
                placement[pair.Key] = pair.Value?.DeepClone();
            return placement;
        }

        private static object[] Case(string layer, string patch) => new object[] { layer, patch };

        private static JsonObject Parse(string json) => JsonNode.Parse(json).AsObject();

        private static JsonNode Find(JsonObject obj, string name)
            => obj.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

        /// <summary>Order-independent text of a tree that keeps <c>null</c> apart from a missing property.</summary>
        private static string Canonical(JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    return "{" + string.Join(",", obj.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(p => p.Key.ToLowerInvariant() + ":" + Canonical(p.Value))) + "}";
                case JsonArray array:
                    return "[" + string.Join(",", array.Select(Canonical)) + "]";
                case null:
                    return "null";
                default:
                    return node.ToJsonString();
            }
        }
    }
}
