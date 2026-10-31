namespace Engine.Tests.Utils
{
    using System.Linq;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;
    using Engine.Tests.Utils.PatchSamples;

    public class JsonLeafPathsTests
    {
        [Fact]
        public void Leaves_UseDotsIndexesAndDeclaredKeys()
        {
            var node = JsonNode.Parse("""{"Id":"h","Parts":[{"Id":"roof","Image":"r"}],"Commands":["a","b"],"Variants":{"w":{"Parts":[{"Id":"p","X":1}]}},"Empty":{},"N":null,"K$add":[1]}""");

            var leaves = JsonLeafPaths.Collect(node, typeof(PatchObjectSample));

            Assert.Equal(new[] { "Id", "Parts[Id=\"roof\"].Id", "Parts[Id=\"roof\"].Image", "Commands[0]", "Commands[1]", "Variants.w.Parts[Id=\"p\"].Id", "Variants.w.Parts[Id=\"p\"].X", "Empty", "N" },
                leaves.Keys.ToArray());
            Assert.True(leaves.ContainsKey("parts[id=\"roof\"].image"));
            Assert.Equal("r", leaves["Parts[Id=\"roof\"].Image"].GetValue<string>());
        }

        [Fact]
        public void Untyped_ArraysUseIndexes_ElementWithoutTheKeyUsesItsIndex()
        {
            Assert.Equal(new[] { "Parts[0].Id" }, JsonLeafPaths.Collect(JsonNode.Parse("""{"Parts":[{"Id":"x"}]}"""), null).Keys.ToArray());
            Assert.Equal(new[] { "Parts[0].Image" }, JsonLeafPaths.Collect(JsonNode.Parse("""{"Parts":[{"Image":"x"}]}"""), typeof(PatchObjectSample)).Keys.ToArray());
        }

        [Fact]
        public void Scalar_IsOneLeafWithAnEmptyPath()
        {
            Assert.Equal(new[] { string.Empty }, JsonLeafPaths.Collect(JsonNode.Parse("5"), null).Keys.ToArray());
        }
    }
}
