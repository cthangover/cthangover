using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Events;
using Cthangover.Core.Quests;
using Cthangover.Core.Scenes;

namespace Engine.Tests.Scenarios
{
    [Collection("ConditionStateMonitor")]
    public class ConditionStateMonitorTests
    {
        [Fact]
        public void AddTag_NewTag_BumpsVersion()
        {
            var quest = new QuestBase { ID = "VerQ1", Name = "VerQ1" };
            var before = ConditionStateMonitor.Version;
            quest.AddTag("t");
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void AddTag_DuplicateTag_DoesNotBump()
        {
            var quest = new QuestBase { ID = "VerQ2", Name = "VerQ2" };
            quest.AddTag("t");
            var before = ConditionStateMonitor.Version;
            quest.AddTag("t");
            Assert.Equal(before, ConditionStateMonitor.Version);
        }

        [Fact]
        public void RemoveTag_Existing_BumpsVersion()
        {
            var quest = new QuestBase { ID = "VerQ3", Name = "VerQ3" };
            quest.AddTag("t");
            var before = ConditionStateMonitor.Version;
            quest.RemoveTag("t");
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void RemoveTag_Absent_DoesNotBump()
        {
            var quest = new QuestBase { ID = "VerQ4", Name = "VerQ4" };
            var before = ConditionStateMonitor.Version;
            quest.RemoveTag("absent");
            Assert.Equal(before, ConditionStateMonitor.Version);
        }

        [Fact]
        public void SetFlag_NewValue_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ScenarioCondition.SetFlag("monFlag1", "a");
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void SetFlag_SameValue_DoesNotBump()
        {
            ScenarioCondition.SetFlag("monFlag2", "a");
            var before = ConditionStateMonitor.Version;
            ScenarioCondition.SetFlag("monFlag2", "a");
            Assert.Equal(before, ConditionStateMonitor.Version);
        }

        [Fact]
        public void Data_SetStatus_Changed_Bumps()
        {
            var data = new QuestData();
            data.SetStatus(1);
            var before = ConditionStateMonitor.Version;
            data.SetStatus(2);
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void Data_SetStatus_Same_DoesNotBump()
        {
            var data = new QuestData();
            data.SetStatus(1);
            var before = ConditionStateMonitor.Version;
            data.SetStatus(1);
            Assert.Equal(before, ConditionStateMonitor.Version);
        }

        [Fact]
        public void Notify_AnyEvent_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ConditionStateMonitor.Notify(new QuestTagChangedEvent { QuestId = "x", Tag = "t", Added = true });
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void MarkStateChanged_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ConditionStateMonitor.MarkStateChanged();
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void MapZoneChangedEvent_Published_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ModEventBus.Publish(new MapZoneChangedEvent { ZoneId = "z", Change = MapZoneChangeKind.Open });
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void SetAll_NonEmpty_Bumps()
        {
            RegisterTestQuest("SaveQ", QuestStatus.NotStarted);
            var saveQuest = new QuestBase
            {
                ID = "SaveQ",
                Name = "SaveQ",
                Data = new QuestData(),
                Status = QuestStatus.Progress,
            };
            var before = ConditionStateMonitor.Version;
            QuestFactory.Instance.SetAll(new List<QuestBase> { saveQuest });
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void SetAll_Empty_DoesNotBump()
        {
            RegisterTestQuest("SaveQEmpty", QuestStatus.NotStarted);
            var before = ConditionStateMonitor.Version;
            QuestFactory.Instance.SetAll(new List<QuestBase>());
            Assert.Equal(before, ConditionStateMonitor.Version);
        }

        private static QuestBase RegisterTestQuest(string id, QuestStatus status)
        {
            var quest = new QuestBase
            {
                ID = id,
                Name = $"Test {id}",
            };
            quest.Status = status;

            var factoryType = typeof(QuestFactory);
            var instanceField = factoryType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var lazyValue = instanceField.GetValue(null);
            var factory = lazyValue.GetType().GetProperty("Value").GetValue(lazyValue);

            var isLoadedField = factoryType.GetField("isLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
            isLoadedField.SetValue(factory, true);

            var dataField = factoryType.GetField("dataByID", BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = (IDictionary<string, QuestBase>)dataField.GetValue(factory);
            dict[id] = quest;

            return quest;
        }
    }
}
