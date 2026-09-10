using Cthangover.Core.Items;

namespace Engine.Tests.Items
{
    public class ItemTypeTests
    {
        [Fact]
        public void ItemType_Flags_HasCorrectValues()
        {
            Assert.Equal(0x00000000u, (uint)ItemType.None);
            Assert.Equal(0x00000001u, (uint)ItemType.Quest);
            Assert.Equal(0x00000002u, (uint)ItemType.Used);
            Assert.Equal(0x00000004u, (uint)ItemType.TargetUsed);
            Assert.Equal(0x00000008u, (uint)ItemType.Food);
            Assert.Equal(0x00000010u, (uint)ItemType.Resource);
            Assert.Equal(0x00000020u, (uint)ItemType.Recipe);
            Assert.Equal(0x80000000u, (uint)ItemType.CantDrop);
        }

        [Fact]
        public void ItemType_CombinedFlags_Works()
        {
            var combined = ItemType.Food | ItemType.Used;
            Assert.True(combined.HasFlag(ItemType.Food));
            Assert.True(combined.HasFlag(ItemType.Used));
            Assert.False(combined.HasFlag(ItemType.Quest));
        }
    }
}
