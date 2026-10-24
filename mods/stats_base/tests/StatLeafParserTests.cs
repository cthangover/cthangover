namespace Cthangover.Mods.Stats.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;


    /// <summary>
    /// The stats join the shared condition language: the forms authors write parse into
    /// stats conditions, mix with flags and logic, and broken forms are syntax errors.
    /// </summary>
    public class StatLeafParserTests
    {
        [ModTest("conditions/forms_parse_into_stat_conditions")]
        static void FormsParse(ModTestContext ctx)
        {
            var two = ConditionParser.Parse("stat(rpg_hero, str) >= 5") as StatCompareCondition;
            ModAssert.NotNull(two, "stat(who, stat)");
            ModAssert.Equal("rpg_hero", two.Who, "who");
            ModAssert.Equal("str", two.Stat, "stat");
            ModAssert.Equal(">=", two.Op, "op");
            ModAssert.Equal(5, two.Value, "value");

            var one = ConditionParser.Parse("stat(cha) < 3") as StatCompareCondition;
            ModAssert.NotNull(one, "stat(stat)");
            ModAssert.Null(one.Who, "the player");

            var quoted = ConditionParser.Parse("stat('village/slime', atk) == 2") as StatCompareCondition;
            ModAssert.Equal("village/slime", quoted?.Who, "quoted id with a slash");

            var level = ConditionParser.Parse("level() > 1") as StatCompareCondition;
            ModAssert.NotNull(level, "level()");
            ModAssert.Null(level.Stat, "level compares no stat");

            var archetype = ConditionParser.Parse("archetype(rpg_hero, mage)") as ArchetypeCondition;
            ModAssert.Equal("mage", archetype?.Archetype, "archetype(who, id)");
        }

        [ModTest("conditions/mix_with_flags_and_logic")]
        static void MixWithFlags(ModTestContext ctx)
        {
            var node = ConditionParser.Parse("door == \"open\" && (stat(str) >= 10 || !archetype(rogue))");
            ModAssert.NotNull(node, "parsed");
        }

        [ModTest("conditions/broken_forms_are_errors")]
        static void BrokenForms(ModTestContext ctx)
        {
            foreach (var broken in new[] { "stat(a, b, c) > 1", "stat(str)", "level(a, b) > 1", "stat(str) >= x", "archetype()" })
            {
                var threw = false;
                try
                {
                    ConditionParser.Parse(broken);
                }
                catch (System.FormatException)
                {
                    threw = true;
                }
                ModAssert.True(threw, $"'{broken}' is a syntax error");
            }
        }
    }
}
