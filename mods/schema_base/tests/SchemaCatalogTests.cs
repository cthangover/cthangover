namespace Cthangover.Mods.Schemas.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Merging schema files of several mods: the later mod replaces an action or command
    /// whole and a kind's field by key, and adds fields of its own; broken files are reported.
    /// </summary>
    public class SchemaCatalogTests
    {
        private const string First = "{\"Actions\":[{\"Id\":\"x.do\",\"Params\":[{\"Key\":\"a\"}]}],\"Kinds\":[{\"Kind\":\"item\",\"Name\":\"k/item\",\"Folder\":\"items\",\"Fields\":[{\"Key\":\"Name\",\"Type\":\"locale\"},{\"Key\":\"Cost\",\"Type\":\"text\"}]}]}";
        private const string Second = "{\"actions\":[{\"id\":\"x.do\",\"params\":[{\"key\":\"b\",\"type\":\"int\"}]}],\"kinds\":[{\"kind\":\"ITEM\",\"fields\":[{\"key\":\"cost\",\"type\":\"int\"},{\"key\":\"Equip\"}]}],\"commands\":[{\"id\":\"check\",\"arrow\":\"k/arrow\"}]}";

        [ModTest("catalog/later_mod_wins_and_fields_merge")]
        static void LaterModWinsAndFieldsMerge(ModTestContext ctx)
        {
            var catalog = SchemaCatalog.Build(new List<(string, string, string)> { ("mod_a", "schemas/a.json", First), ("mod_b", "schemas/b.json", Second) });

            ModAssert.Equal(0, catalog.Errors.Count, "both files parse (keys in any case)");
            var action = catalog.Actions["X.DO"];
            ModAssert.Equal("mod_b", action.SourceMod, "the later action replaced the earlier");
            ModAssert.Equal(1, action.Params.Count, "replaced whole, not merged");
            ModAssert.Equal("b", action.Params[0].Key, "the later parameter");

            var item = catalog.Kinds["item"];
            ModAssert.Equal("k/item", item.Name, "a kind keeps the name nobody replaced");
            ModAssert.Equal("items", item.Folder, "and the folder");
            ModAssert.Equal(3, item.Fields.Count, "Name, Cost, Equip");
            ModAssert.Equal("Name", item.Fields[0].Key, "order kept");
            ModAssert.Equal("int", item.Fields[1].Type, "a field with the same key is replaced in place");
            ModAssert.Equal("mod_b", item.Fields[1].SourceMod, "and remembers who described it");
            ModAssert.Equal("Equip", item.Fields[2].Key, "a new field is appended");
            ModAssert.Equal("mod_a", item.Fields[0].SourceMod, "the untouched field keeps its source");
            ModAssert.NotNull(catalog.Commands["check"], "commands are read too");
        }

        [ModTest("catalog/broken_files_are_reported")]
        static void BrokenFilesAreReported(ModTestContext ctx)
        {
            var catalog = SchemaCatalog.Build(new List<(string, string, string)> { ("mod_a", "schemas/a.json", "{ not json"), ("mod_b", "schemas/b.json", First) });

            ModAssert.Equal(1, catalog.Errors.Count, "one broken file");
            ModAssert.True(catalog.Errors[0].StartsWith("mod_a:schemas/a.json"), "named by mod and path");
            ModAssert.NotNull(catalog.Actions["x.do"], "the good file still counts");
        }

        [ModTest("catalog/loaded_mods_describe_their_actions_and_kinds")]
        static void LoadedModsDescribe(ModTestContext ctx)
        {
            var catalog = SchemaCatalog.Current;
            ModAssert.Equal(0, catalog.Errors.Count, "every shipped schema parses: " + string.Join("; ", catalog.Errors));
            foreach (var id in new[] { "stat.add", "stat.exp", "equip.put", "item.add", "rpg.battle", "trade.open" })
                ModAssert.True(catalog.Actions.ContainsKey(id), $"action {id} is described");
            foreach (var id in new[] { "check", "when", "text", "switch_scene" })
                ModAssert.True(catalog.Commands.ContainsKey(id), $"command {id} is described");
            var item = catalog.FieldsOf("item").Select(f => f.Key).ToList();
            ModAssert.True(item.Contains("Cost") && item.Contains("Equip") && item.Contains("Traits"), "items: the tools' own fields and the equipment mod's merged");
            var character = catalog.FieldsOf("character").Select(f => f.Key).ToList();
            ModAssert.True(character.Contains("Health") && character.Contains("Archetype") && character.Contains("Stats"), "characters: builtin and stats fields merged");
            ModAssert.True(catalog.FieldsOf("action").Any(f => f.Key == "Values" && f.Of.Any(o => o.Key == "Formula")), "battle formulas describe action values");
        }
    }
}
