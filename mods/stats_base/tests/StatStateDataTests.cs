namespace Cthangover.Mods.Stats.Tests
{
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;

    /// <summary>The saved stats survive a JSON round trip in every field.</summary>
    public class StatStateDataTests
    {
        [ModTest("state/json_round_trip")]
        static void JsonRoundTrip(ModTestContext ctx)
        {
            var original = new StatStateData();
            var hero = new CharacterStatState { Archetype = "warrior", Level = 3, Exp = 61 };
            hero.Bonus["str"] = 2;
            hero.Bonus["cha"] = -1.5;
            hero.Pools["mp"] = 4;
            original.Characters["rpg_hero"] = hero;

            var restored = JsonSerializer.Deserialize<StatStateData>(JsonSerializer.Serialize(original));

            var back = restored.Characters["rpg_hero"];
            ModAssert.Equal("warrior", back.Archetype, "archetype");
            ModAssert.Equal(3, back.Level, "level");
            ModAssert.Equal(61, back.Exp, "exp");
            ModAssert.CloseTo(2, back.Bonus["str"], what: "bonus str");
            ModAssert.CloseTo(-1.5, back.Bonus["cha"], what: "negative bonus");
            ModAssert.CloseTo(4, back.Pools["mp"], what: "pool");
        }

        [ModTest("state/of_creates_find_ignores_case")]
        static void OfCreatesFindIgnoresCase(ModTestContext ctx)
        {
            var state = new StatState();
            ModAssert.Null(state.Find("Hero"), "nothing yet");
            var created = state.Of("Hero");
            ModAssert.True(ReferenceEquals(created, state.Of("hero")), "the same entry in any case");
            ModAssert.True(ReferenceEquals(created, state.Find("HERO")), "find ignores case");
        }
    }
}
