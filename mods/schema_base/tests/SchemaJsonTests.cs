namespace Cthangover.Mods.Schemas.Tests
{
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Form text and JSON nodes convert into the shapes the engine's readers expect.</summary>
    public class SchemaJsonTests
    {
        [ModTest("json/typed_nodes")]
        static void TypedNodes(ModTestContext ctx)
        {
            ModAssert.Equal("5", SchemaJson.ToNode(new FieldSchema { Type = "int" }, "5").ToJsonString(), "int");
            ModAssert.Equal("2.5", SchemaJson.ToNode(new FieldSchema { Type = "number" }, "2.5").ToJsonString(), "number");
            ModAssert.Equal("true", SchemaJson.ToNode(new FieldSchema { Type = "bool" }, "1").ToJsonString(), "bool");
            ModAssert.Equal("[\"a\",\"b\"]", SchemaJson.ToNode(new FieldSchema { Type = "ref_list" }, "a, b").ToJsonString(), "list of ids");
            ModAssert.Equal("str + 1", SchemaJson.ToNode(new FieldSchema { Type = "formula" }, " str + 1 ").GetValue<string>(), "formula text, trimmed");
            ModAssert.Null(SchemaJson.ToNode(new FieldSchema { Type = "int" }, "  "), "empty removes the key");
            ModAssert.Equal("7", SchemaJson.NumberOrText("7").ToJsonString(), "a map value that is a number");
            ModAssert.Equal("6 + level", SchemaJson.NumberOrText("6 + level").GetValue<string>(), "a map value that is a formula");
        }

        [ModTest("json/texts_for_the_form")]
        static void TextsForTheForm(ModTestContext ctx)
        {
            var node = JsonNode.Parse("{\"a\": 3, \"b\": true, \"c\": [\"x\", \"y\"], \"d\": \"t\", \"e\": {\"k\": 1}}");
            ModAssert.Equal("3", SchemaJson.ToText(node["a"]), "number");
            ModAssert.Equal("1", SchemaJson.ToText(node["b"]), "true");
            ModAssert.Equal("x,y", SchemaJson.ToText(node["c"]), "array of strings");
            ModAssert.Equal("t", SchemaJson.ToText(node["d"]), "string");
            ModAssert.Equal("{\"k\":1}", SchemaJson.ToText(node["e"]), "object as JSON");
            ModAssert.Equal("", SchemaJson.ToText(null), "absent");
        }
    }
}
