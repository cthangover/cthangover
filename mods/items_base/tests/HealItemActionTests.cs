namespace Cthangover.Mods.Items.Tests
{
    using Cthangover.Core.Characters;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;

    /// <summary>
    /// <c>action/heal</c>: the amount comes from the item's <c>heal</c> property, health is
    /// capped at its base value, and an item that does nothing is reported as not used.
    /// </summary>
    public class HealItemActionTests
    {
        private static Item Food(int heal)
        {
            var properties = new PropertyData();
            properties.SetInt(HealItemAction.HealProperty, heal);
            return new Item { ID = "test/food", Properties = properties };
        }

        private static CharacterInfoData Hero(int health, int max)
        {
            var attributes = new CharacterAttributes();
            attributes.Health.Value = health;
            attributes.Health.BaseValue = max;
            return new CharacterInfoData { ID = "hero", Attributes = attributes };
        }

        [ModTest("heal/restores_the_amount")]
        static void RestoresTheAmount(ModTestContext ctx)
        {
            var hero = Hero(10, 24);
            ModAssert.True(new HealItemAction().UseOn(Food(5), hero), "wounded hero is healed");
            ModAssert.Equal(15, hero.Attributes.Health.Value, "10 + 5");
            ModAssert.Equal(24, hero.Attributes.Health.BaseValue, "max unchanged");
        }

        [ModTest("heal/capped_at_max")]
        static void CappedAtMax(ModTestContext ctx)
        {
            var hero = Hero(22, 24);
            ModAssert.True(new HealItemAction().UseOn(Food(20), hero), "partly wounded hero is healed");
            ModAssert.Equal(24, hero.Attributes.Health.Value, "never above max");
        }

        [ModTest("heal/useless_heal_is_not_used")]
        static void UselessHealIsNotUsed(ModTestContext ctx)
        {
            var action = new HealItemAction();

            var full = Hero(24, 24);
            ModAssert.False(action.UseOn(Food(5), full), "full health");
            ModAssert.Equal(24, full.Attributes.Health.Value, "full stays full");

            var fallen = Hero(0, 24);
            ModAssert.False(action.UseOn(Food(5), fallen), "a fallen hero is not revived");
            ModAssert.Equal(0, fallen.Attributes.Health.Value, "fallen stays fallen");

            var wounded = Hero(10, 24);
            ModAssert.False(action.UseOn(Food(0), wounded), "no heal amount");
            ModAssert.False(action.UseOn(new Item { ID = "plain" }, wounded), "no properties");
            ModAssert.False(action.UseOn(Food(5), null), "no target");
            ModAssert.False(action.UseOn(Food(5), new CharacterInfoData { ID = "bare" }), "no attributes");
            ModAssert.False(action.UseAction(Food(5)), "heal needs a target");
            ModAssert.Equal(10, wounded.Attributes.Health.Value, "untouched");
        }

        [ModTest("heal/id")]
        static void Id(ModTestContext ctx)
        {
            ModAssert.Equal("action/heal", new HealItemAction().ID, "item definitions reference this id");
        }
    }
}
