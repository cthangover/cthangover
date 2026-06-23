using Cthangover.Core.Utils;

namespace Engine.Tests.Utils
{
    public class ListsTests
    {
        [Fact]
        public void IsEmpty_NullCollection_ReturnsTrue()
        {
            Assert.True(Lists.IsEmpty<object>(null));
        }

        [Fact]
        public void IsEmpty_EmptyList_ReturnsTrue()
        {
            Assert.True(Lists.IsEmpty(new List<int>()));
        }

        [Fact]
        public void IsEmpty_NonEmptyList_ReturnsFalse()
        {
            Assert.False(Lists.IsEmpty(new List<int> { 1 }));
        }

        [Fact]
        public void IsNotEmpty_NullCollection_ReturnsFalse()
        {
            Assert.False(Lists.IsNotEmpty<object>(null));
        }

        [Fact]
        public void IsNotEmpty_NonEmptyList_ReturnsTrue()
        {
            Assert.True(Lists.IsNotEmpty(new List<int> { 1 }));
        }

        [Fact]
        public void Singleton_ReturnsCollectionWithOneItem()
        {
            var result = Lists.Singleton(42);
            Assert.Single(result);
            Assert.Equal(42, result.First());
        }
    }
}
