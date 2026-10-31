namespace Engine.Tests.Mods
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Resolvers;
    using Engine.Tests.Utils.PatchSamples;

    public class EntityExtendsResolutionTests
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public void Child_InheritsParent_OwnValuesWin_NestedListsMergeByKey()
        {
            var resolved = Resolve(
                """{"Id":"door","Name":"Door","Parts":[{"Id":"leaf","Image":"wood","X":2},{"Id":"frame","Image":"stone"}],"Commands":["a"]}""",
                """{"Id":"door_red","Extends":"door","Parts$add":[{"Id":"leaf","Image":"red"}],"Commands$add":["b"]}""");

            var child = resolved["door_red"];
            Assert.Equal("Door", child.Name);
            Assert.Equal("door", child.Extends);
            Assert.Equal(new[] { "leaf:red:2", "frame:stone:0" }, child.Parts.Select(p => $"{p.Id}:{p.Image}:{p.X}"));
            Assert.Equal(new[] { "a", "b" }, child.Commands);
            Assert.Equal("wood", resolved["door"].Parts[0].Image);
        }

        [Fact]
        public void Chain_ResolvesGrandparentFirst_RegardlessOfOrder()
        {
            var resolved = Resolve(
                """{"Id":"c","Extends":"b","Commands":["c"]}""",
                """{"Id":"b","Extends":"a","Name":"B"}""",
                """{"Id":"a","Name":"A","Parts":[{"Id":"p","Image":"a"}]}""");

            Assert.Equal("B", resolved["c"].Name);
            Assert.Equal("a", Assert.Single(resolved["c"].Parts).Image);
            Assert.Equal(new[] { "c" }, resolved["c"].Commands);
        }

        [Fact]
        public void ParentIdIsCaseInsensitive()
        {
            var resolved = Resolve("""{"Id":"Base","Name":"N"}""", """{"Id":"kid","Extends":"BASE"}""");

            Assert.Equal("N", resolved["kid"].Name);
        }

        [Fact]
        public void Cycle_SkipsItsMembers_KeepsTheRest()
        {
            var resolved = Resolve(
                """{"Id":"a","Extends":"b"}""",
                """{"Id":"b","Extends":"a"}""",
                """{"Id":"free","Name":"F"}""");

            Assert.Equal(new[] { "free" }, resolved.Keys);
        }

        [Fact]
        public void SelfReference_IsACycle()
        {
            Assert.Empty(Resolve("""{"Id":"a","Extends":"a"}"""));
        }

        [Fact]
        public void UnknownParent_OrChildOfACycle_StaysAsWritten()
        {
            var resolved = Resolve(
                """{"Id":"orphan","Extends":"nobody","Name":"O"}""",
                """{"Id":"x","Extends":"y"}""",
                """{"Id":"y","Extends":"x"}""",
                """{"Id":"kid","Extends":"x","Name":"K"}""");

            Assert.Equal(new[] { "orphan", "kid" }, resolved.Keys);
            Assert.Equal("O", resolved["orphan"].Name);
            Assert.Equal("K", resolved["kid"].Name);
        }

        [Fact]
        public void EntitiesWithoutWrittenJson_PassThroughUntouched()
        {
            var item = new PatchObjectSample { Id = "plain", Name = "P" };
            var items = new Dictionary<string, PatchObjectSample> { ["plain"] = item };

            var resolved = new EntityExtendsResolution<PatchObjectSample>("sample", items, null, null).ResolveAll();

            Assert.Same(item, resolved["plain"]);
        }

        [Theory]
        [InlineData("""{"Id":"a","extends":"b"}""", "b")]
        [InlineData("""{"Id":"a","Extends":""}""", null)]
        [InlineData("""{"Id":"a","Extends":5}""", null)]
        [InlineData("""{"Id":"a"}""", null)]
        public void ExtendsOf_ReadsANonEmptyString(string json, string expected)
        {
            Assert.Equal(expected, EntityExtendsResolution<PatchObjectSample>.ExtendsOf(JsonNode.Parse(json).AsObject()));
        }

        [Fact]
        public void ChildVariant_ComposesWithParentVariant_KeepingItsOperations()
        {
            var items = new Dictionary<string, PatchLayeredObjectSample>();
            var written = new Dictionary<string, JsonObject>();
            foreach (var json in new[]
            {
                """{"Id":"tree","Parts":[{"Id":"crown","Image":"green"}],"Variants":{"winter":{"Parts$add":[{"Id":"snow","Image":"white"}]}}}""",
                """{"Id":"pine","Extends":"tree","Variants":{"winter":{"Parts$add":[{"Id":"snow","X":2}]}}}""",
            })
            {
                var item = JsonSerializer.Deserialize<PatchLayeredObjectSample>(json, ReadOptions);
                items[item.Id] = item;
                written[item.Id] = JsonNode.Parse(json).AsObject();
            }

            var resolved = new EntityExtendsResolution<PatchLayeredObjectSample>("sample", items, written, null).ResolveAll();

            var winter = JsonNode.Parse(resolved["pine"].Variants["winter"].GetRawText()).AsObject();
            Assert.False(winter.ContainsKey("Parts"));
            var snow = Assert.Single(winter["Parts$add"].AsArray());
            Assert.Equal("white", snow["Image"].GetValue<string>());
            Assert.Equal(2, snow["X"].GetValue<int>());
        }

        private static Dictionary<string, PatchObjectSample> Resolve(params string[] entities)
        {
            var items = new Dictionary<string, PatchObjectSample>();
            var written = new Dictionary<string, JsonObject>();
            foreach (var json in entities)
            {
                var item = JsonSerializer.Deserialize<PatchObjectSample>(json, ReadOptions);
                items[item.Id] = item;
                written[item.Id] = JsonNode.Parse(json).AsObject();
            }
            return new EntityExtendsResolution<PatchObjectSample>("sample", items, written, null).ResolveAll();
        }
    }
}
