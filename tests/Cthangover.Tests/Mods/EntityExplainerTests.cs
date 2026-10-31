namespace Engine.Tests.Mods
{
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Resolvers;
    using Engine.Tests.Utils.PatchSamples;

    public class EntityExplainerTests
    {
        [Fact]
        public void Definition_OwnsWrittenFields_DefaultsHaveNoOrigin()
        {
            var explanation = Explainer(("door", "mod_a", "objects/door.json", """{"Id":"door","Name":"Door"}""")).Explain("door");

            Assert.Equal("Door", explanation.Value["Name"].GetValue<string>());
            Assert.Equal(new[] { "Id", "Name" }, Sorted(explanation.Fields.Keys));
            var name = explanation.Fields["name"];
            Assert.Equal((EntityLayer.Definition, "door", "mod_a", "objects/door.json"), (name.Layer, name.EntityId, name.ModId, name.File));
            Assert.False(explanation.Deleted);
        }

        [Fact]
        public void Extends_InheritedFieldsKeepTheParentsOrigin_ChildOwnsWhatItWritesOrChanges()
        {
            var explanation = Explainer(
                ("door", "mod_a", "a.json", """{"Id":"door","Name":"Door","Parts":[{"Id":"leaf","Image":"wood","X":2},{"Id":"frame","Image":"stone"}]}"""),
                ("door_red", "mod_b", "b.json", """{"Id":"door_red","Extends":"door","Parts$add":[{"Id":"leaf","Image":"red"}],"Commands$add":["knock"]}""")).Explain("door_red");

            Assert.Equal("a.json", explanation.Fields["Name"].File);
            Assert.Equal("door", explanation.Fields["Name"].EntityId);
            Assert.Equal("b.json", explanation.Fields["Parts[Id=\"leaf\"].Image"].File);
            Assert.Equal("a.json", explanation.Fields["Parts[Id=\"leaf\"].X"].File);
            Assert.Equal("a.json", explanation.Fields["Parts[Id=\"frame\"].Image"].File);
            Assert.Equal("b.json", explanation.Fields["Commands[0]"].File);
            Assert.Equal("door_red", explanation.Fields["Id"].EntityId);
            Assert.Equal("b.json", explanation.Fields["Extends"].File);
        }

        [Fact]
        public void UnknownId_AndCycle_GiveNull_UnknownParentIsAsWritten()
        {
            var explainer = Explainer(
                ("a", "m", "f.json", """{"Id":"a","Extends":"b"}"""),
                ("b", "m", "f.json", """{"Id":"b","Extends":"a"}"""),
                ("orphan", "m", "f.json", """{"Id":"orphan","Extends":"nobody","Name":"O"}"""));

            Assert.Null(explainer.Explain("missing"));
            Assert.Null(explainer.Explain("a"));
            Assert.Equal("O", explainer.Explain("orphan").Value["Name"].GetValue<string>());
        }

        private static EntityExplainer Explainer(params (string id, string mod, string file, string json)[] definitions)
        {
            var map = new Dictionary<string, EntityDefinitionSource>();
            foreach (var (id, mod, file, json) in definitions)
                map[id] = new EntityDefinitionSource { ModId = mod, File = file, Written = JsonNode.Parse(json).AsObject() };
            return new EntityExplainer("sample", typeof(PatchObjectSample), map, null);
        }

        private static string[] Sorted(IEnumerable<string> keys)
        {
            var list = new List<string>(keys);
            list.Sort(System.StringComparer.Ordinal);
            return list.ToArray();
        }
    }
}
