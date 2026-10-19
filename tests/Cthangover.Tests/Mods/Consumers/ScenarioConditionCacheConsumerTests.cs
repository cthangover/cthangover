namespace Cthangover.Tests.Mods.Consumers
{
    using Cthangover.Core.Mods.Consumers;
    using Cthangover.Core.Scenes;

    /// <summary>
    /// The kernel consumer that drops parsed scenario conditions before a mod
    /// assembly unloads: conditions keep evaluating live state around it.
    /// </summary>
    public class ScenarioConditionCacheConsumerTests
    {
        [Fact]
        public void OnAssemblyUnloading_CachedConditions_StillEvaluate()
        {
            var consumer = new ScenarioConditionCacheConsumer();
            ScenarioCondition.SetFlag("consumerCacheFlag", "1");
            Assert.True(ScenarioCondition.Evaluate("consumerCacheFlag == \"1\" || false"));

            consumer.OnAssemblyUnloading(typeof(ScenarioConditionCacheConsumerTests).Assembly, "test_mod");

            Assert.True(ScenarioCondition.Evaluate("consumerCacheFlag == \"1\" || false"));
            ScenarioCondition.SetFlag("consumerCacheFlag", "2");
            Assert.False(ScenarioCondition.Evaluate("consumerCacheFlag == \"1\" || false"));
        }

        [Fact]
        public void OnAssemblyLoaded_LeavesConditionsWorking()
        {
            var consumer = new ScenarioConditionCacheConsumer();

            consumer.OnAssemblyLoaded(typeof(ScenarioConditionCacheConsumerTests).Assembly, "test_mod");

            Assert.True(ScenarioCondition.Evaluate("true && !false"));
        }

        [Fact]
        public void Id_IsStableAndNotEmpty()
        {
            Assert.Equal("scenario_condition_cache", new ScenarioConditionCacheConsumer().Id);
        }
    }
}
