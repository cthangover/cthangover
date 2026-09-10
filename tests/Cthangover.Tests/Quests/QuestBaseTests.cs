using Cthangover.Core.Quests;

namespace Engine.Tests.Quests
{
    public class QuestBaseTests
    {
        private readonly QuestBase _quest = new QuestBase
        {
            ID = "TestQuest",
            Name = "Test Quest",
            StatusToDescription = new Dictionary<int, string> { { 0, "Not started" } },
        };

        [Fact]
        public void QuestBase_DefaultStatus_NotStarted()
        {
            var quest = new QuestBase();
            Assert.Equal(QuestStatus.NotStarted, quest.Status);
        }

        [Fact]
        public void QuestBase_Id_ReturnsSetValue()
        {
            Assert.Equal("TestQuest", _quest.ID);
        }

        [Fact]
        public void QuestBase_ContainsTag_WhenNotAdded_ReturnsFalse()
        {
            Assert.False(_quest.ContainsTag("some_tag"));
        }

        [Fact]
        public void QuestBase_AddTag_ThenContainsTag_ReturnsTrue()
        {
            _quest.AddTag("some_tag");
            Assert.True(_quest.ContainsTag("some_tag"));
        }

        [Fact]
        public void QuestBase_NotContainsTag_ReturnsOpposite()
        {
            Assert.True(_quest.NotContainsTag("missing"));
            _quest.AddTag("present");
            Assert.False(_quest.NotContainsTag("present"));
        }

        [Fact]
        public void QuestBase_RemoveTag_Works()
        {
            _quest.AddTag("temp");
            Assert.True(_quest.ContainsTag("temp"));

            _quest.RemoveTag("temp");
            Assert.False(_quest.ContainsTag("temp"));
        }

        [Fact]
        public void QuestBase_CanChangeStatus()
        {
            _quest.Status = QuestStatus.Progress;
            Assert.Equal(QuestStatus.Progress, _quest.Status);

            _quest.Status = QuestStatus.End;
            Assert.Equal(QuestStatus.End, _quest.Status);
        }

        [Fact]
        public void QuestBase_StatusToDescription_ContainsExpected()
        {
            var descriptions = _quest.StatusToDescription;
            Assert.Contains(0, descriptions.Keys);
            Assert.Equal("Not started", descriptions[0]);
        }
    }
}
