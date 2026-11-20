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

        // There is deliberately no "CombinedFlags_Works" test: ItemType is a
        // bare [Flags] enum without methods, so combining and HasFlag belong to
        // the BCL. A test of Enum.HasFlag semantics stays green even if two
        // flag values collide — the value pinning above is the real guard.
    }
}
