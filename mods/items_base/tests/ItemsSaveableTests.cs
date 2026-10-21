namespace Cthangover.Mods.Items.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;

    /// <summary>
    /// The items slot across a change of the game's content: a save may name
    /// an item no loaded mod defines any more (its mod was removed or the id
    /// renamed). Loading it must keep the rest of the inventory usable, and
    /// the next save must still be written — with the unknown stack kept, so
    /// it comes back with its mod.
    /// </summary>
    public class ItemsSaveableTests
    {
        private const string UnknownId = "__test/no_such_item";

        [ModTest("save/unknown_item_survives_load_and_save")]
        static void UnknownItemSurvivesLoadAndSave(ModTestContext ctx)
        {
            var known = Entities.Collect<ItemInfo>().Keys.First();
            var runtime = GameData.Instance.Runtime;
            var original = runtime.Inventory;
            try
            {
                runtime.Inventory = new Inventory();
                var slot = new ModSlot();
                slot.Data["Inventory"] = new DataBlob(1, new List<CItem>
                {
                    new() { ID = known, Count = 4 },
                    new() { ID = UnknownId, Count = 3 },
                });
                var saveable = new ItemsSaveable();

                ModAssert.True(saveable.OnLoad(slot, out _), "slot with an unknown item loads");
                ModAssert.Equal(4, runtime.Inventory.CheckCount(known), "known stack restored");
                ModAssert.False(runtime.Inventory.HasItem(UnknownId), "unknown item is not a usable stack");

                var saved = new ModSlot();
                saveable.OnSave(saved);
                var items = saved.Data["Inventory"].ReadJson<List<CItem>>();
                ModAssert.Equal(4, items.Single(i => i.ID == known).Count, "known stack saved");
                ModAssert.Equal(3, items.Single(i => i.ID == UnknownId).Count, "unknown stack kept in the save");
            }
            finally
            {
                runtime.Inventory = original;
            }
        }
    }
}
