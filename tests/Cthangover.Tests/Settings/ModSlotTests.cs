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
            Assert.True(slot.Metadata.IsRequired);
        }

        [Fact]
        public void DataBlob_stored_in_slot_is_read_back_with_same_values()
        {
            var slot = new ModSlot();
            var blob = new DataBlob(2);
            blob.SetInt("x", 10);
            blob.SetString("y", "test");
            slot.Data["block1"] = blob;

            Assert.Equal(10, slot.Data["block1"].GetInt("x"));
            Assert.Equal("test", slot.Data["block1"].GetString("y"));
        }
    }
}
