namespace Cthangover.Mods.BattleFormulas.Tests
{
    using Cthangover.Core.Characters;
    using Cthangover.Core.Mods.Testing;

    /// <summary>a.* reads the one acting, b.* the target; live attributes under fixed names; other prefixes are unknown.</summary>
    public class BattleFormulaScopeTests
    {
        [ModTest("scope/sides_and_live_names")]
        static void SidesAndLiveNames(ModTestContext ctx)
        {
            var a = new Character { ID = "test_a_no_stats", Level = 4 };
            a.Attributes.Health.Init(40);
            a.Attributes.Health.Value = 25;
            a.Attributes.Attack.Init(7);
            var b = new Character { ID = "test_b_no_stats", Level = 2 };
            b.Attributes.Defence.Init(3);
            b.Attributes.Point.Init(2);

            var scope = new BattleFormulaScope(a, b);

            ModAssert.True(scope.TryGet("a.hp", out var hp) && hp == 25, "a.hp is the current health");
            ModAssert.True(scope.TryGet("a.hp_max", out var max) && max == 40, "a.hp_max");
            ModAssert.True(scope.TryGet("a.attack", out var atk) && atk == 7, "a.attack");
            ModAssert.True(scope.TryGet("a.level", out var level) && level == 4, "a.level from the battle character");
            ModAssert.True(scope.TryGet("b.defence", out var def) && def == 3, "b.defence");
            ModAssert.True(scope.TryGet("b.point", out var points) && points == 2, "b.point");
            ModAssert.False(scope.TryGet("c.hp", out _), "unknown side");
            ModAssert.False(scope.TryGet("hp", out _), "no side");
            ModAssert.False(scope.TryGet("b.no_such_stat", out _), "a stat nobody has");
        }
    }
}
