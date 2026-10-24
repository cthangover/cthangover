namespace Cthangover.Mods.Stats.Tests
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// How a sheet is computed: own value over archetype over formula over default, then
    /// bonuses and traits, then the bounds — and derived stats reading other stats.
    /// </summary>
    public class StatCalculatorTests
    {
        private static Dictionary<string, StatInfo> Defs(params StatInfo[] stats)
        {
            var defs = new Dictionary<string, StatInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in stats)
                defs[s.ID] = s;
            return defs;
        }

        [ModTest("calculator/precedence_own_archetype_formula_default")]
        static void Precedence(ModTestContext ctx)
        {
            var inputs = new StatInputs
            {
                Level = 3,
                Definitions = Defs(
                    new StatInfo { ID = "str", Default = 5 },
                    new StatInfo { ID = "agi", Default = 5, Formula = "4 + level" },
                    new StatInfo { ID = "int", Default = 5, Formula = "2 + level" },
                    new StatInfo { ID = "luk", Default = 5 }),
                Own = new Dictionary<string, string> { ["str"] = "12" },
                Archetype = new ArchetypeInfo { ID = "rogue", Stats = new Dictionary<string, string> { ["agi"] = "6 + level * 2", ["str"] = "1" } },
            };

            var sheet = StatCalculator.Calculate("hero", inputs);

            ModAssert.CloseTo(12, sheet.Get("str"), what: "own value beats the archetype");
            ModAssert.CloseTo(12, sheet.Get("agi"), what: "archetype beats the stat formula");
            ModAssert.CloseTo(5, sheet.Get("int"), what: "stat formula beats the default");
            ModAssert.CloseTo(5, sheet.Get("luk"), what: "default");
            ModAssert.Equal("rogue", sheet.ArchetypeId, "archetype id");
            ModAssert.Equal(3, sheet.Level, "level");
        }

        [ModTest("calculator/derived_stats_read_others_in_any_order")]
        static void DerivedStats(ModTestContext ctx)
        {
            var inputs = new StatInputs
            {
                Level = 2,
                Definitions = Defs(
                    new StatInfo { ID = "max_hp", Formula = "30 + vit * 6 + level * 4" },
                    new StatInfo { ID = "atk", Formula = "str * 2" },
                    new StatInfo { ID = "vit", Default = 3 },
                    new StatInfo { ID = "str", Default = 4 }),
            };

            var sheet = StatCalculator.Calculate("hero", inputs);

            ModAssert.CloseTo(56, sheet.Get("max_hp"), what: "30 + 18 + 8");
            ModAssert.CloseTo(8, sheet.Get("atk"), what: "atk from str");
        }

        [ModTest("calculator/bonus_traits_and_bounds")]
        static void BonusTraitsAndBounds(ModTestContext ctx)
        {
            var inputs = new StatInputs
            {
                Level = 4,
                Definitions = Defs(
                    new StatInfo { ID = "atk", Default = 10 },
                    new StatInfo { ID = "luk", Default = 5, Max = 9 },
                    new StatInfo { ID = "def", Default = 2, Min = 0 }),
                Bonus = new Dictionary<string, double> { ["atk"] = 2 },
                Traits = new List<TraitInfo>
                {
                    new() { Stat = "atk", Add = 3 },
                    new() { Stat = "atk", Percent = 50 },
                    new() { Stat = "atk", Formula = "level" },
                    new() { Stat = "luk", Add = 10 },
                    new() { Stat = "def", Add = -8 },
                },
            };

            var sheet = StatCalculator.Calculate("hero", inputs);

            ModAssert.CloseTo(12, sheet.GetBase("atk"), what: "base = default + bonus");
            ModAssert.CloseTo(28.5, sheet.Get("atk"), what: "(12 + 3 + 4) * 1.5");
            ModAssert.Equal(29, sheet.Rounded("atk"), "rounded half away from zero");
            ModAssert.CloseTo(9, sheet.Get("luk"), what: "clamped to Max");
            ModAssert.CloseTo(0, sheet.Get("def"), what: "clamped to Min");
        }

        [ModTest("calculator/undefined_stats_still_count")]
        static void UndefinedStatsStillCount(ModTestContext ctx)
        {
            var inputs = new StatInputs
            {
                Own = new Dictionary<string, string> { ["courage"] = "3" },
                Traits = new List<TraitInfo> { new() { Stat = "trust", Add = 2 } },
            };

            var sheet = StatCalculator.Calculate("player", inputs);

            ModAssert.True(sheet.Has("courage") && sheet.Has("trust"), "own and trait stats are on the sheet");
            ModAssert.CloseTo(3, sheet.Get("courage"), what: "own undefined stat");
            ModAssert.CloseTo(2, sheet.Get("trust"), what: "trait-only stat");
            ModAssert.CloseTo(0, sheet.Get("nothing"), what: "unknown stat reads 0");
        }

        [ModTest("calculator/cycle_reads_zero_instead_of_hanging")]
        static void CycleReadsZero(ModTestContext ctx)
        {
            var inputs = new StatInputs
            {
                Definitions = Defs(
                    new StatInfo { ID = "a", Formula = "b + 1" },
                    new StatInfo { ID = "b", Formula = "a + 1" }),
            };

            var sheet = StatCalculator.Calculate("loop", inputs);

            ModAssert.True(sheet.Has("a") && sheet.Has("b"), "both computed");
            ModAssert.True(sheet.Get("a") <= 2 && sheet.Get("b") <= 2, "the looping read counted as 0");
        }

        [ModTest("calculator/sheet_is_a_formula_scope")]
        static void SheetIsAScope(ModTestContext ctx)
        {
            var sheet = StatCalculator.Calculate("hero", new StatInputs
            {
                Level = 5,
                Definitions = Defs(new StatInfo { ID = "str", Default = 7 }),
            });

            ModAssert.CloseTo(17, Formulas.Formula.Evaluate("str + level * 2", sheet), what: "sheet answers stats and level");
        }
    }
}
