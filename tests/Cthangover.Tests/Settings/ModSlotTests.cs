using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    public class ModSlotTests
    {
        [Fact]
        public void New_slot_has_empty_data_and_default_metadata()
        {
            var slot = new ModSlot();
            Assert.NotNull(slot.Metadata);
            Assert.NotNull(slot.Data);
            Assert.Empty(slot.Data);
        }

        // ModSlot is a plain data holder (two auto-properties, no logic): the
        // DataBlob key-value contract it stores is covered by DataBlobTests and
        // its persistence path by RuntimeSerializerTests, so a test that puts a
        // DataBlob into the dictionary and reads the same instance back would
        // only re-verify those suites plus the BCL dictionary.
    }
}
