namespace Cthangover.Mods.Schemas.Tests
{
    using System.Linq;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Which entity references a value makes, by its field type.</summary>
    public class SchemaReferencesTests
    {
        private static string Refs(FieldSchema field, string value)
            => string.Join(";", SchemaReferences.Of(field, value).Select(r => r.Kind + ":" + r.Id));

        [ModTest("references/by_type")]
        static void ByType(ModTestContext ctx)
        {
            ModAssert.Equal("item:sword", Refs(new FieldSchema { Type = "ref", Kind = "item" }, " sword "), "ref");
            ModAssert.Equal("character:a;character:b", Refs(new FieldSchema { Type = "ref_list", Kind = "character" }, "a, b,"), "ref_list");
            ModAssert.Equal("character:hero", Refs(new FieldSchema { Type = "who" }, "hero"), "who names a character");
            ModAssert.Equal("", Refs(new FieldSchema { Type = "who" }, "party"), "party is no reference");
            ModAssert.Equal("", Refs(new FieldSchema { Type = "who" }, "Player"), "player is no reference");
            ModAssert.Equal("scene:town", Refs(new FieldSchema { Type = "scene" }, "town"), "scene");
            ModAssert.Equal("", Refs(new FieldSchema { Type = "text" }, "anything"), "text makes none");
            ModAssert.Equal("", Refs(new FieldSchema { Type = "ref" }, "x"), "a ref without a kind makes none");
            ModAssert.Equal("", Refs(new FieldSchema { Type = "ref", Kind = "item" }, "${picked}"), "a run-time value makes none");
        }
    }
}
