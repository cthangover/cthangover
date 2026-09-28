using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Quests;

namespace Engine.Tests.Quests
{
    /// <summary>
    /// Covers <see cref="QuestFactory"/>'s save round-trip and new-game reset:
    /// <c>SetAll</c> must restore registered quests and tolerate entries from
    /// content that no longer exists, while <c>ResetAll</c> must wipe progress
    /// so a second playthrough starts clean. Both mutate the process-wide
    /// singleton, so the class joins the non-parallel ConditionStateMonitor
    /// collection.
    /// </summary>
    [Collection("ConditionStateMonitor")]
    public class QuestFactoryTests
    {
        [Fact]
        public void SetAll_RegisteredQuest_RestoresStatusTagsAndStage()
        {
            var quest = Register("FactorySave");
            var saved = new QuestBase
            {
                ID = "FactorySave",
                Name = "Test",
                Status = QuestStatus.Progress,
                Data = new QuestData { Status = 3 },
            };
            saved.Data.Tags.Add("tag");

            QuestFactory.Instance.SetAll(new List<QuestBase> { saved });

            Assert.Equal(QuestStatus.Progress, quest.Status);
            Assert.Equal(3, quest.Data.Status);
            Assert.True(quest.ContainsTag("tag"));
        }

        [Fact]
        public void SetAll_UnregisteredQuest_SkipsWithoutThrowing()
        {
            var quest = Register("FactoryKnown");
            var saved = new List<QuestBase>
            {
                new QuestBase { ID = "FactoryGone", Name = "Gone", Status = QuestStatus.End },
                new QuestBase { ID = "FactoryKnown", Name = "Test", Status = QuestStatus.Progress },
            };

            QuestFactory.Instance.SetAll(saved);

            Assert.Equal(QuestStatus.Progress, quest.Status);
        }

        [Fact]
        public void ResetAll_WipesStatusTagsAndStage()
        {
            var quest = Register("FactoryReset");
            quest.Status = QuestStatus.End;
            quest.AddTag("earned");
            quest.Data.SetStatus(5);

            QuestFactory.Instance.ResetAll();

            Assert.Equal(QuestStatus.NotStarted, quest.Status);
            Assert.Empty(quest.Data.Tags);
            Assert.Equal(0, quest.Data.Status);
        }

        private static QuestBase Register(string id)
        {
            var quest = new QuestBase { ID = id, Name = "Test " + id };
            var factory = GetFactory();
            SetPrivate(factory, "isLoaded", true);
            var dict = (IDictionary<string, QuestBase>)GetPrivate(factory, "dataByID");
            dict[id] = quest;
            return quest;
        }

        private static object GetFactory()
        {
            var lazyField = typeof(QuestFactory).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var lazy = lazyField.GetValue(null);
            return lazy.GetType().GetProperty("Value").GetValue(lazy);
        }

        private static object GetPrivate(object factory, string fieldName)
        {
            return typeof(QuestFactory).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(factory);
        }

        private static void SetPrivate(object factory, string fieldName, object value)
        {
            typeof(QuestFactory).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(factory, value);
        }
    }
}
