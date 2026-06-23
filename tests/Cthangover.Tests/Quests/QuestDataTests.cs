using Cthangover.Core.Quests;

namespace Engine.Tests.Quests
{
    public class QuestDataTests
    {
        [Fact]
        public void QuestData_DefaultValues()
        {
            var data = new QuestData();
            Assert.Equal(0, data.Status);
            Assert.NotNull(data.Tags);
            Assert.Empty(data.Tags);
        }

        [Fact]
        public void QuestData_SetStatus_UpdatesValue()
        {
            var data = new QuestData();
            data.SetStatus(5);
            Assert.Equal(5, data.Status);
        }

        [Fact]
        public void QuestData_Tags_CanAddAndContain()
        {
            var data = new QuestData();
            data.Tags.Add("completed");
            Assert.Contains("completed", data.Tags);
            Assert.Single(data.Tags);
        }
    }
}
