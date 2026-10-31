namespace Engine.Tests.Utils
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Patches;
    using Engine.Tests.Utils.PatchSamples;

    public class MergeTypeMapTests
    {
        [Fact]
        public void ChildType_ByJsonNameOrPropertyName_CaseInsensitive()
        {
            Assert.Equal(typeof(List<PatchCellSample>), MergeTypeMap.ChildType(typeof(PatchObjectSample), "overrides"));
            Assert.Null(MergeTypeMap.ChildType(typeof(PatchObjectSample), "Cells"));
            Assert.Equal(typeof(PatchLightSample), MergeTypeMap.ChildType(typeof(PatchTileSample), "LIGHT"));
            Assert.Equal(typeof(int), MergeTypeMap.ChildType(typeof(PatchTileSample), "Index"));
            Assert.Null(MergeTypeMap.ChildType(typeof(PatchTileSample), "Unknown"));
            Assert.Null(MergeTypeMap.ChildType(null, "Light"));
        }

        [Fact]
        public void ChildType_OfDictionary_IsItsValueType_UntypedValuesHaveNone()
        {
            Assert.Equal(typeof(PatchObjectSample), MergeTypeMap.ChildType(typeof(Dictionary<string, PatchObjectSample>), "winter"));
            Assert.Null(MergeTypeMap.ChildType(typeof(Dictionary<string, object>), "x"));
        }

        [Fact]
        public void ItemType_OfListsAndArrays()
        {
            Assert.Equal(typeof(PatchPartSample), MergeTypeMap.ItemType(typeof(List<PatchPartSample>)));
            Assert.Equal(typeof(int), MergeTypeMap.ItemType(typeof(int[])));
            Assert.Equal(typeof(int), MergeTypeMap.ItemType(typeof(IEnumerable<int?>)));
            Assert.Null(MergeTypeMap.ItemType(typeof(string)));
            Assert.Null(MergeTypeMap.ItemType(typeof(PatchPartSample)));
            Assert.Null(MergeTypeMap.ItemType(null));
        }

        [Fact]
        public void KeyOf_FromAttributeOrIMergeable_NeverGuessed()
        {
            Assert.Equal(new[] { "Id" }, MergeTypeMap.KeyOf(typeof(PatchObjectSample), "parts"));
            Assert.Equal(new[] { "Layer", "X", "Y" }, MergeTypeMap.KeyOf(typeof(PatchObjectSample), "Overrides"));
            Assert.Equal(new[] { "Index" }, MergeTypeMap.KeyOf(typeof(PatchTilesetSample), "Tiles"));
            Assert.Null(MergeTypeMap.KeyOf(typeof(PatchObjectSample), "Commands"));
            Assert.Null(MergeTypeMap.KeyOf(typeof(PatchObjectSample), "Variants"));
            Assert.Null(MergeTypeMap.KeyOf(null, "Parts"));
        }
    }
}
