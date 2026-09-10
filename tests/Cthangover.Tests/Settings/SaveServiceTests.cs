using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    public class SaveServiceTests
    {
        [Fact]
        public void MergeCompletedScenarioIds_NullInputs_ReturnsEmpty()
        {
            var result = SaveService.MergeCompletedScenarioIds(null, null);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void MergeCompletedScenarioIds_RuntimeHistoryOnly()
        {
            var runtime = new RuntimeData();
            runtime.CompletedScenarioIds.Add("morning_wake");

            var result = SaveService.MergeCompletedScenarioIds(null, runtime);

            Assert.Contains("morning_wake", result);
        }

        [Fact]
        public void MergeCompletedScenarioIds_LiveChainOnly()
        {
            var result = SaveService.MergeCompletedScenarioIds(new HashSet<string> { "bar_fight" }, new RuntimeData());

            Assert.Contains("bar_fight", result);
        }

        [Fact]
        public void MergeCompletedScenarioIds_UnionOfBothSources()
        {
            var runtime = new RuntimeData();
            runtime.CompletedScenarioIds.Add("morning_wake");
            var liveIds = new HashSet<string> { "morning_wake", "alley_note" };

            var result = SaveService.MergeCompletedScenarioIds(liveIds, runtime);

            Assert.Equal(2, result.Count);
            Assert.Contains("morning_wake", result);
            Assert.Contains("alley_note", result);
        }

        [Fact]
        public void MergeCompletedScenarioIds_DoesNotMutateRuntimeHistory()
        {
            var runtime = new RuntimeData();
            runtime.CompletedScenarioIds.Add("morning_wake");
            var liveIds = new HashSet<string> { "alley_note" };

            SaveService.MergeCompletedScenarioIds(liveIds, runtime);

            Assert.Single(runtime.CompletedScenarioIds);
        }
    }
}
