namespace Cthangover.Tools.Schemas.Tests
{
    using Cthangover.Mods.Schemas;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Dialog.Scenarios;

    /// <summary>Lines the palette writes are DSL the scenario parser reads back with the same values.</summary>
    public class SchemaLineTests
    {
        private static readonly ActionSchema Battle = new()
        {
            Id = "rpg.battle",
            Params = new List<FieldSchema> { new() { Key = "enemies" }, new() { Key = "win_flag" }, new() { Key = "note" } },
        };

        private static readonly CommandSchema Check = new()
        {
            Id = "check",
            Positional = new List<FieldSchema> { new() { Key = "stat" }, new() { Key = "difficulty" } },
            Params = new List<FieldSchema> { new() { Key = "else" } },
            Arrow = "k",
        };

        [ModTest("line/action_order_and_empties")]
        static void ActionOrderAndEmpties(ModTestContext ctx)
        {
            var line = SchemaLine.Action(Battle, new Dictionary<string, string> { ["win_flag"] = "won", ["enemies"] = "a,b", ["note"] = "" });
            ModAssert.Equal("action rpg.battle enemies=a,b win_flag=won", line, "schema order, empty left out");
        }

        [ModTest("line/round_trip_through_the_parser")]
        static void RoundTrip(ModTestContext ctx)
        {
            var line = SchemaLine.Action(Battle, new Dictionary<string, string> { ["enemies"] = "rpg_wolf", ["note"] = "two \"words\"" });
            var positional = new List<string>();
            var named = new Dictionary<string, string>();
            var command = ScenarioParser.SplitLine(line, positional, named, out _);
            ModAssert.Equal("action", command, "command");
            ModAssert.Equal("rpg.battle", positional[0], "action name");
            ModAssert.Equal("rpg_wolf", named["enemies"], "plain value");
            ModAssert.Equal("two \"words\"", named["note"], "a value with spaces and quotes survives");

            var check = SchemaLine.Command(Check, new Dictionary<string, string> { ["stat"] = "cha", ["difficulty"] = "12", ["else"] = "fail" }, ":ok");
            ModAssert.Equal("check cha 12 else=fail -> :ok", check, "command line");
            positional.Clear();
            named.Clear();
            ScenarioParser.SplitLine(check, positional, named, out var arrow);
            ModAssert.Equal("12", positional[1], "positional");
            ModAssert.Equal("ok", arrow, "arrow");
        }

        [ModTest("line/quote")]
        static void QuoteRules(ModTestContext ctx)
        {
            ModAssert.Equal("plain", SchemaLine.Quote("plain"), "no quotes needed");
            ModAssert.Equal("\"a b\"", SchemaLine.Quote("a b"), "spaces");
            ModAssert.Equal("\"\"", SchemaLine.Quote(""), "empty");
            ModAssert.Equal("\"say \\\"hi\\\"\"", SchemaLine.Quote("say \"hi\""), "inner quotes escaped");
        }
    }
}
