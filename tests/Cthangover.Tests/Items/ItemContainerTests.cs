using Cthangover.Core.Items;

namespace Engine.Tests.Items
{
    public class ItemContainerTests
    {
        [Fact]
        public void ItemContainer_DefaultValues()
        {
            var container = new ItemContainer();
            Assert.Null(container.Item);
            Assert.Equal(0, container.Count);
        }

        [Fact]
        public void ItemContainer_CanSetProperties()
        {
            var container = new ItemContainer
            {
                Count = 5
            };
            Assert.Equal(5, container.Count);
        }
    }
}
