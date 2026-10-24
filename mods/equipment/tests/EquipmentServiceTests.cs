namespace Cthangover.Mods.Equipment.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;

    /// <summary>
    /// The data rules of equipment that need no game: which slot takes which item type,
    /// the saved state's round trip, the condition syntax. The equip flow with real items,
    /// the inventory and battle attributes is covered by the rpg_plus game test.
    /// </summary>
    public class EquipmentServiceTests
    {
        [ModTest("slots/accepts_by_type_or_by_slot_id")]
        static void AcceptsByTypeOrSlotId(ModTestContext ctx)
        {
            var hand = new EquipSlotInfo { ID = "weapon", Accepts = new List<string> { "sword", "Staff" } };
            var body = new EquipSlotInfo { ID = "body" };

            ModAssert.True(EquipmentService.Accepts(hand, "sword"), "listed type");
            ModAssert.True(EquipmentService.Accepts(hand, "staff"), "listed type in any case");
            ModAssert.False(EquipmentService.Accepts(hand, "weapon"), "a slot with a list takes only the list");
            ModAssert.True(EquipmentService.Accepts(body, "BODY"), "a slot without a list takes its own id");
            ModAssert.False(EquipmentService.Accepts(body, null), "no type is no gear");
            ModAssert.False(EquipmentService.Accepts(null, "sword"), "no slot");
        }

        [ModTest("state/json_round_trip")]
        static void JsonRoundTrip(ModTestContext ctx)
        {
            var original = new EquipmentStateData();
            original.Characters["rpg_hero"] = new Dictionary<string, string> { ["weapon"] = "rpg_plus/iron_sword", ["body"] = "rpg_plus/leather_armor" };

            var restored = JsonSerializer.Deserialize<EquipmentStateData>(JsonSerializer.Serialize(original));

            ModAssert.Equal(2, restored.Characters["rpg_hero"].Count, "two slots");
            ModAssert.Equal("rpg_plus/iron_sword", restored.Characters["rpg_hero"]["weapon"], "weapon");
            ModAssert.Equal("rpg_plus/leather_armor", restored.Characters["rpg_hero"]["body"], "body");
        }

        [ModTest("conditions/equipped_parses")]
        static void EquippedParses(ModTestContext ctx)
        {
            var two = ConditionParser.Parse("equipped(rpg_hero, 'rpg_plus/iron_sword')") as EquippedCondition;
            ModAssert.Equal("rpg_hero", two?.Who, "who");
            ModAssert.Equal("rpg_plus/iron_sword", two?.Item, "quoted item");

            var one = ConditionParser.Parse("!equipped(lucky_charm)") ;
            ModAssert.NotNull(one, "negated one-argument form");

            var threw = false;
            try
            {
                ConditionParser.Parse("equipped()");
            }
            catch (System.FormatException)
            {
                threw = true;
            }
            ModAssert.True(threw, "no argument is an error");
        }

        [ModTest("worn/nobody_wears_anything_after_reset")]
        static void NobodyWearsAfterReset(ModTestContext ctx)
        {
            var saved = EquipmentService.Data;
            try
            {
                EquipmentService.Data = new EquipmentStateData();
                EquipmentService.Data.Characters["tester"] = new Dictionary<string, string> { ["weapon"] = "x" };
                ModAssert.True(EquipmentService.Wears("tester", "X"), "wears, item id in any case");
                ModAssert.Equal("x", EquipmentService.ItemIn("TESTER", "weapon"), "lookup by id in any case");
                EquipmentService.Reset();
                ModAssert.Equal(0, EquipmentService.Worn("tester").Count, "reset empties");
            }
            finally
            {
                EquipmentService.Data = saved;
            }
        }
    }
}
