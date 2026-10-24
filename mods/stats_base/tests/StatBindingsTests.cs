namespace Cthangover.Mods.Stats.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// The bridge to battles: an attribute's maximum follows the stat, its current value
    /// moves by the same amount, and a fallen character is never revived by a sync.
    /// </summary>
    public class StatBindingsTests
    {
        private static readonly StatInfo[] Defs =
        {
            new() { ID = "max_hp", Binds = "health" },
            new() { ID = "atk", Binds = "attack" },
            new() { ID = "luk" },
        };

        private static StatSheet Sheet(double hp, double atk = 0)
        {
            var values = new Dictionary<string, double> { ["max_hp"] = hp, ["atk"] = atk, ["luk"] = 99 };
            return new StatSheet("hero", 1, null, values, values);
        }

        [ModTest("bindings/find_by_name_and_alias")]
        static void FindByNameAndAlias(ModTestContext ctx)
        {
            var attributes = new CharacterAttributes();
            ModAssert.True(ReferenceEquals(attributes.Health, StatBindings.Find(attributes, "Health")), "health");
            ModAssert.True(ReferenceEquals(attributes.Attack, StatBindings.Find(attributes, "atk")), "atk alias");
            ModAssert.True(ReferenceEquals(attributes.Defence, StatBindings.Find(attributes, "defense")), "defense spelling");
            ModAssert.True(ReferenceEquals(attributes.Point, StatBindings.Find(attributes, "point")), "point");
            ModAssert.Null(StatBindings.Find(attributes, "mana"), "unknown name");
        }

        [ModTest("bindings/loaded_stats_bind_known_attributes")]
        static void LoadedStatsBindKnownAttributes(ModTestContext ctx)
        {
            foreach (var def in StatsService.Definitions)
            {
                if (def.Binds != null)
                    ModAssert.NotNull(StatBindings.Find(new CharacterAttributes(), def.Binds), $"stat '{def.ID}' binds '{def.Binds}'");
            }
        }

        [ModTest("bindings/current_moves_with_max")]
        static void CurrentMovesWithMax(ModTestContext ctx)
        {
            var attributes = new CharacterAttributes();
            attributes.Health.Init(30);
            attributes.Health.Value = 20;
            attributes.Attack.Init(6);

            var changed = StatBindings.Apply(Sheet(36, 9), attributes, Defs);

            ModAssert.Equal(2, changed, "health and attack changed, luk binds nothing");
            ModAssert.Equal(36, attributes.Health.BaseValue, "max follows the stat");
            ModAssert.Equal(26, attributes.Health.Value, "a wounded hero gains the same 6");
            ModAssert.Equal(9, attributes.Attack.Value, "attack follows");
            ModAssert.Equal(0, StatBindings.Apply(Sheet(36, 9), attributes, Defs), "nothing changes twice");

            StatBindings.Apply(Sheet(10, 9), attributes, Defs);
            ModAssert.Equal(10, attributes.Health.Value, "current capped by a lower max");

            StatBindings.Apply(Sheet(4, 9), attributes, Defs);
            ModAssert.Equal(4, attributes.Health.Value, "still alive at the new max");
        }

        [ModTest("bindings/fallen_stay_fallen_unset_is_filled")]
        static void FallenStayFallen(ModTestContext ctx)
        {
            var attributes = new CharacterAttributes();
            attributes.Health.Init(30);
            attributes.Health.Value = 0;
            StatBindings.Apply(Sheet(50), attributes, Defs);
            ModAssert.Equal(0, attributes.Health.Value, "a fallen hero stays fallen");
            ModAssert.Equal(50, attributes.Health.BaseValue, "but the max still follows");

            var fresh = new CharacterAttributes();
            StatBindings.Apply(Sheet(44.6), fresh, Defs);
            ModAssert.Equal(45, fresh.Health.Value, "an unset attribute is filled, rounded");
            ModAssert.Equal(45, fresh.Health.BaseValue, "with its max");
        }
    }
}
