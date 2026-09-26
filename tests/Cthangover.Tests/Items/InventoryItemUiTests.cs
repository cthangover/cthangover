using Cthangover.Core.Items;
using Cthangover.Core.UI.Inventory;
using Cthangover.Core.Utils;
using Godot;

namespace Engine.Tests.Items
{
    /// <summary>
    /// Covers the presentation decision table of <see cref="InventoryItemUi"/>:
    /// which flags make an item usable/droppable, which verb and tag the UI
    /// resolves for a given type combination, and which accent colour wins
    /// when flags overlap. These rules are shared by the grid cell and the
    /// passport, so a regression here silently desyncs the two.
    /// </summary>
    public class InventoryItemUiTests
    {
        private static Item MakeItem(ItemType type, bool withAction = false)
        {
            return new Item
            {
                ID = "test/item",
                Name = "items/test/name",
                Description = "items/test/desc",
                ItemType = type,
                ItemAction = withAction ? new StubItemAction() : null,
                Properties = new PropertyData(),
            };
        }

        [Fact]
        public void IsUsable_NullItem_IsFalse()
        {
            Assert.False(InventoryItemUi.IsUsable(null));
        }

        [Fact]
        public void IsUsable_UsedFlagWithoutAction_IsFalse()
        {
            Assert.False(InventoryItemUi.IsUsable(MakeItem(ItemType.Used)));
        }

        [Fact]
        public void IsUsable_FoodWithAction_IsFalse()
        {
            Assert.False(InventoryItemUi.IsUsable(MakeItem(ItemType.Food, true)));
        }

        [Fact]
        public void IsUsable_UsedWithAction_IsTrue()
        {
            Assert.True(InventoryItemUi.IsUsable(MakeItem(ItemType.Used, true)));
        }

        [Fact]
        public void IsUsable_TargetUsedWithAction_IsTrue()
        {
            Assert.True(InventoryItemUi.IsUsable(MakeItem(ItemType.TargetUsed, true)));
        }

        [Fact]
        public void CanDrop_NullItem_IsFalse()
        {
            Assert.False(InventoryItemUi.CanDrop(null));
        }

        [Fact]
        public void CanDrop_CantDropFlag_IsFalse()
        {
            Assert.False(InventoryItemUi.CanDrop(MakeItem(ItemType.Quest | ItemType.CantDrop)));
        }

        [Fact]
        public void CanDrop_RegularItem_IsTrue()
        {
            Assert.True(InventoryItemUi.CanDrop(MakeItem(ItemType.Resource)));
        }

        [Fact]
        public void VerbKey_PropertyOverride_Wins()
        {
            var item = MakeItem(ItemType.Food | ItemType.Used, true);
            item.Properties.SetStr("verb", "Выпить");

            Assert.Equal("Выпить", InventoryItemUi.VerbKey(item));
        }

        [Fact]
        public void VerbKey_Recipe_ReturnsRecipeVerb()
        {
            Assert.Equal("ui/inventory/verb_recipe", InventoryItemUi.VerbKey(MakeItem(ItemType.Recipe | ItemType.Used, true)));
        }

        [Fact]
        public void VerbKey_TargetUsed_ReturnsTargetVerb()
        {
            Assert.Equal("ui/inventory/verb_target", InventoryItemUi.VerbKey(MakeItem(ItemType.TargetUsed, true)));
        }

        [Fact]
        public void VerbKey_Food_ReturnsEatVerb()
        {
            Assert.Equal("ui/inventory/verb_eat", InventoryItemUi.VerbKey(MakeItem(ItemType.Food)));
        }

        [Fact]
        public void VerbKey_Default_ReturnsUseVerb()
        {
            Assert.Equal("ui/inventory/verb_use", InventoryItemUi.VerbKey(MakeItem(ItemType.Resource)));
        }

        [Fact]
        public void TagKey_NoAction_IsNull()
        {
            Assert.Null(InventoryItemUi.TagKey(MakeItem(ItemType.Food)));
        }

        [Fact]
        public void TagKey_Recipe_ReturnsRecipeTag()
        {
            Assert.Equal("ui/inventory/tag_recipe", InventoryItemUi.TagKey(MakeItem(ItemType.Recipe | ItemType.Used, true)));
        }

        [Fact]
        public void TagKey_TargetUsed_ReturnsTargetTag()
        {
            Assert.Equal("ui/inventory/tag_target", InventoryItemUi.TagKey(MakeItem(ItemType.TargetUsed, true)));
        }

        [Fact]
        public void TagKey_FoodWithAction_ReturnsFoodTag()
        {
            Assert.Equal("ui/inventory/tag_food", InventoryItemUi.TagKey(MakeItem(ItemType.Food | ItemType.Used, true)));
        }

        [Fact]
        public void TagKey_GenericUsable_ReturnsUseTag()
        {
            Assert.Equal("ui/inventory/tag_use", InventoryItemUi.TagKey(MakeItem(ItemType.Used, true)));
        }

        [Fact]
        public void AccentColor_QuestDominatesOverFood()
        {
            Assert.Equal(Color.FromHtml("d9a441"), InventoryItemUi.AccentColor(ItemType.Quest | ItemType.Food));
        }

        [Fact]
        public void AccentColor_FoodDominatesOverResource()
        {
            Assert.Equal(Color.FromHtml("8fae6b"), InventoryItemUi.AccentColor(ItemType.Food | ItemType.Resource));
        }

        [Fact]
        public void AccentColor_TargetUsed_ReturnsTeal()
        {
            Assert.Equal(Color.FromHtml("4fb3a5"), InventoryItemUi.AccentColor(ItemType.TargetUsed));
        }

        [Fact]
        public void AccentColor_UnknownType_ReturnsMuted()
        {
            Assert.Equal(Color.FromHtml("8b8894"), InventoryItemUi.AccentColor(ItemType.None));
        }

        [Fact]
        public void TypeBadgeKey_MapsEveryFlag()
        {
            Assert.Equal("ui/inventory/type_quest", InventoryItemUi.TypeBadgeKey(ItemType.Quest));
            Assert.Equal("ui/inventory/type_used", InventoryItemUi.TypeBadgeKey(ItemType.Used));
            Assert.Equal("ui/inventory/type_target", InventoryItemUi.TypeBadgeKey(ItemType.TargetUsed));
            Assert.Equal("ui/inventory/type_food", InventoryItemUi.TypeBadgeKey(ItemType.Food));
            Assert.Equal("ui/inventory/type_resource", InventoryItemUi.TypeBadgeKey(ItemType.Resource));
            Assert.Equal("ui/inventory/type_recipe", InventoryItemUi.TypeBadgeKey(ItemType.Recipe));
            Assert.Equal("ui/inventory/type_cant_drop", InventoryItemUi.TypeBadgeKey(ItemType.CantDrop));
            Assert.Null(InventoryItemUi.TypeBadgeKey(ItemType.None));
        }
    }
}
