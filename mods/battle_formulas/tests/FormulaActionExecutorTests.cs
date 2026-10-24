namespace Cthangover.Mods.BattleFormulas.Tests
{
    using Cthangover.Core.Characters;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;

    /// <summary>
    /// A formula action applied to two plain characters: damage through shields, heals
    /// capped at the maximum, refused actions changing nothing.
    /// </summary>
    public class FormulaActionExecutorTests
    {
        private static Character Fighter(string id, int hp, int attack = 0, int defence = 0, int points = 3)
        {
            var c = new Character { ID = id, Level = 1 };
            c.Attributes.Health.Init(hp);
            c.Attributes.Attack.Init(attack);
            c.Attributes.Defence.Init(defence);
            c.Attributes.Point.Init(points);
            return c;
        }

        private static ActionCharacter Action(ActionCharacterType type, params string[] values)
        {
            var props = new PropertyData();
            foreach (var value in values)
            {
                var eq = value.IndexOf('=');
                props.Values[value.Substring(0, eq)] = value.Substring(eq + 1);
            }
            return new ActionCharacter { ID = "test/formula", Type = type, Properties = props };
        }

        [ModTest("executor/damage_from_live_attributes")]
        static void DamageFromLiveAttributes(ModTestContext ctx)
        {
            var hero = Fighter("test_hero_no_stats", 20, attack: 10);
            var slime = Fighter("test_slime_no_stats", 30);

            var result = new FormulaActionExecutor("test/formula").Execute(Action(ActionCharacterType.ToEnemy, "Formula=a.attack * 2 - 3"), hero, slime);

            ModAssert.True(result.Result, "applied");
            ModAssert.Equal(17, result.Target.Damage, "10 * 2 - 3");
            ModAssert.Equal(13, slime.Attributes.Health.Value, "health lost");
        }

        [ModTest("executor/shield_absorbs_unless_pierce")]
        static void ShieldAbsorbsUnlessPierce(ModTestContext ctx)
        {
            var hero = Fighter("test_hero_no_stats", 20);
            var guard = Fighter("test_guard_no_stats", 30, defence: 5);

            var blocked = new FormulaActionExecutor("test/formula").Execute(Action(ActionCharacterType.ToEnemy, "Formula=12"), hero, guard);
            ModAssert.Equal(7, blocked.Target.Damage, "the shield took 5");
            ModAssert.Equal(-5, blocked.Target.Defence, "shield change");
            ModAssert.Equal(0, guard.Attributes.Defence.Value, "the shield is spent");

            guard.Attributes.Defence.Value = 5;
            var pierced = new FormulaActionExecutor("test/formula").Execute(Action(ActionCharacterType.ToEnemy, "Formula=12", "Pierce=1"), hero, guard);
            ModAssert.Equal(12, pierced.Target.Damage, "pierce ignores the shield");
            ModAssert.Equal(5, guard.Attributes.Defence.Value, "the shield is kept");
        }

        [ModTest("executor/heal_is_capped_and_targets_self")]
        static void HealIsCapped(ModTestContext ctx)
        {
            var hero = Fighter("test_hero_no_stats", 20);
            hero.Attributes.Health.Value = 15;

            var result = new FormulaActionExecutor("test/formula").Execute(Action(ActionCharacterType.ToSelf, "Formula=10", "Effect=heal"), hero, null);

            ModAssert.True(result.Result, "applied");
            ModAssert.Equal(5, result.Target.Damage, "only the missing 5 are healed");
            ModAssert.Equal(20, hero.Attributes.Health.Value, "full");
        }

        [ModTest("executor/points_refuse_and_negative_is_zero")]
        static void PointsRefuseAndNegativeIsZero(ModTestContext ctx)
        {
            var hero = Fighter("test_hero_no_stats", 20, points: 1);
            var slime = Fighter("test_slime_no_stats", 30);

            var refused = new FormulaActionExecutor("test/formula").Execute(Action(ActionCharacterType.ToEnemy, "Formula=50", "RequiredPoint=2"), hero, slime);
            ModAssert.False(refused.Result, "not enough action points");
            ModAssert.Equal(30, slime.Attributes.Health.Value, "nothing happened");
            ModAssert.Equal(1, hero.Attributes.Point.Value, "no points spent");

            var zero = new FormulaActionExecutor("test/formula").Execute(Action(ActionCharacterType.ToEnemy, "Formula=b.hp - 999"), hero, slime);
            ModAssert.Equal(0, zero.Target.Damage, "a negative amount is 0");
            ModAssert.Equal(30, slime.Attributes.Health.Value, "never heals by accident");
        }

        [ModTest("registry/formula_actions_are_detected")]
        static void FormulaActionsAreDetected(ModTestContext ctx)
        {
            ModAssert.True(FormulaActions.IsFormulaAction(new ActionCharacterInfo { ID = "x", Values = new() { "RequiredPoint=1", "formula=a.attack" } }), "Formula= in any case");
            ModAssert.False(FormulaActions.IsFormulaAction(new ActionCharacterInfo { ID = "y", Values = new() { "Attack=2" } }), "a core action");
            ModAssert.False(FormulaActions.IsFormulaAction(new ActionCharacterInfo { ID = "z" }), "no values");
        }
    }
}
