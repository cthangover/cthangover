using Cthangover.Core.Events;
using Cthangover.Core.Scenes;

namespace Engine.Tests.Scenarios
{
    /// <summary>
    /// Cache-invalidation monitor of <see cref="ScenarioCondition"/>: any
    /// quest/flag/map-zone change must bump the version so cached condition
    /// ASTs re-evaluate. Quest state mutation (tag/status/data changes) is
    /// owned by the quests_base engine mod; the core keeps the event
    /// contracts and the subscription — those are covered here via direct
    /// event publishes. End-to-end quest mutations are covered by the
    /// integration coverage test (coverage_quests) with mods loaded.
    /// </summary>
    [Collection("ConditionStateMonitor")]
    public class ConditionStateMonitorTests
    {
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
        public void Notify_AnyEvent_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ConditionStateMonitor.Notify(new QuestTagChangedEvent { QuestId = "x", Tag = "t", Added = true });
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void QuestStatusChangedEvent_Published_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ModEventBus.Publish(new QuestStatusChangedEvent { QuestId = "x", OldStatus = "NotStarted", NewStatus = "Progress" });
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void QuestDataStatusChangedEvent_Published_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ModEventBus.Publish(new QuestDataStatusChangedEvent { Old = 1, New = 2 });
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void QuestStateRestoredEvent_Published_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ModEventBus.Publish(new QuestStateRestoredEvent());
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }

        [Fact]
        public void MarkStateChanged_Bumps()
        {
            var before = ConditionStateMonitor.Version;
            ConditionStateMonitor.MarkStateChanged();
            Assert.Equal(before + 1, ConditionStateMonitor.Version);
        }
    }
}
