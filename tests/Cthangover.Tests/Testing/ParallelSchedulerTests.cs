namespace Cthangover.Tests.Testing
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.TestRunner;

    /// <summary>Scheduling contracts use simulated children: limits, ready copies, dependency state and deterministic result order.</summary>
    [Collection("runner-infrastructure")]
    public class ParallelSchedulerTests
    {
        [Fact]
        public void GlobalGuiAndExclusiveLimitsApplyTogether()
        {
            var plan = new List<TestPlanEntry>();
            for (var i = 0; i < 16; i++) plan.Add(Entry("case" + i, i % 2 == 0 ? "write" : "read", gui: i % 3 == 0));
            var executor = new FakeParallelExecutor();
            var results = ParallelScheduler.Run(plan, Options(4, 1, 2), executor);
            Assert.Equal(4, executor.MaxActive);
            Assert.Equal(1, executor.MaxGui);
            Assert.Equal(2, executor.MaxWriters);
            Assert.False(executor.LaneOverlap);
            Assert.All(results, result => Assert.True(result.Passed));
            Assert.Single(executor.CompletionThreads);
        }

        [Fact]
        public void ChainKeepsOneExclusiveLaneAndDoesNotInterleaveOtherComponents()
        {
            var setup = Entry("setup", "read", seconds: 100);
            var child = Entry("child", "write", environment: "setup", dependencies: new[] { "setup" });
            var other = Entry("other", "write");
            var executor = new FakeParallelExecutor();
            ParallelScheduler.Run(new[] { setup, child, other }, Options(4, 2, 1), executor);
            Assert.Equal(new[] { "setup", "child", "other" }, executor.Starts);
            Assert.Equal(0, executor.Lanes["setup"]);
            Assert.Equal(executor.Lanes["setup"], executor.Lanes["child"]);
            Assert.False(executor.ChainLostState);
            Assert.False(executor.LaneOverlap);
        }

        [Fact]
        public void FailedPreparationBlocksDescendantsButAllowsIndependentWork()
        {
            var executor = new FakeParallelExecutor();
            executor.Failures.Add("setup");
            var results = ParallelScheduler.Run(new[] { Entry("setup", "write"),
                Entry("child", "read", environment: "setup", dependencies: new[] { "setup" }),
                Entry("grandchild", "read", environment: "setup", dependencies: new[] { "child" }), Entry("other", "read") }, Options(4, 2, 1), executor);
            Assert.False(results[0].Passed);
            Assert.True(results[1].Blocked);
            Assert.True(results[2].Blocked);
            Assert.True(results[3].Passed);
            Assert.DoesNotContain("child", executor.Starts);
            Assert.DoesNotContain("grandchild", executor.Starts);
            Assert.Equal("dependency", results[1].FailureKind);
        }

        [Fact]
        public void LongestFirstExecutionStillReturnsPlanOrder()
        {
            var executor = new FakeParallelExecutor();
            var results = ParallelScheduler.Run(new[] { Entry("short", "read", seconds: 1), Entry("long", "read", seconds: 300),
                Entry("middle", "read", seconds: 20) }, Options(1, 1, 1), executor);
            Assert.Equal(new[] { "long", "middle", "short" }, executor.Starts);
            Assert.Equal(new[] { "short", "long", "middle" }, results.Select(result => result.Name));
        }

        [Fact]
        public void SharedLaunchDoesNotWaitForAnUnrelatedSlowCopy()
        {
            var executor = new FakeParallelExecutor { SlowLane = 0 };
            var results = ParallelScheduler.Run(new[] { Entry("writer", "write", seconds: 200), Entry("reader", "read") }, Options(2, 1, 1), executor);
            Assert.Equal("reader", executor.Starts[0]);
            Assert.True(executor.StartedBeforeSlowCopy);
            Assert.All(results, result => Assert.True(result.Passed));
        }

        [Fact]
        public void MissingDurationUsesAQuarterOfTheTimeout()
        {
            var fallback = Entry("fallback", "read");
            fallback.Test.Seconds = null;
            fallback.Test.TimeoutSeconds = 100;
            var executor = new FakeParallelExecutor();
            ParallelScheduler.Run(new[] { Entry("measured", "read", seconds: 20), fallback }, Options(1, 1, 1), executor);
            Assert.Equal(new[] { "fallback", "measured" }, executor.Starts);
        }

        [Fact]
        public void InfrastructureFailureCancelsAndDrainsOtherLaunches()
        {
            var executor = new FakeParallelExecutor { ThrowOn = "broken" };
            executor.Delays["other"] = 10000;
            executor.Delays["broken"] = 100;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.Throws<System.InvalidOperationException>(() => ParallelScheduler.Run(
                new[] { Entry("broken", "read"), Entry("other", "read") }, Options(2, 1, 1), executor));
            Assert.Equal(0, executor.Active);
            Assert.InRange(watch.Elapsed.TotalSeconds, 0, 2);
        }

        [Fact]
        public void AChainWaitingForItsGuiChildStillHoldsItsWritableCopy()
        {
            var executor = new FakeParallelExecutor();
            executor.Delays["gui"] = 250;
            var results = ParallelScheduler.Run(new[] { Entry("gui", "read", gui: true, seconds: 1000),
                Entry("setup", "read", seconds: 100), Entry("child", "write", gui: true, environment: "setup", dependencies: new[] { "setup" }),
                Entry("other", "write", seconds: 90) }, Options(3, 1, 1), executor);
            Assert.True(executor.Starts.IndexOf("child") < executor.Starts.IndexOf("other"));
            Assert.False(executor.ChainLostState);
            Assert.Equal(executor.Lanes["setup"], executor.Lanes["child"]);
            Assert.All(results, result => Assert.True(result.Passed));
        }

        private static RunnerOptions Options(int jobs, int gui, int write) => RunnerOptions.Parse(new[] {
            "--jobs", jobs.ToString(), "--gui-jobs", gui.ToString(), "--write-jobs", write.ToString() });

        private static TestPlanEntry Entry(string name, string access, bool gui = false, double seconds = 10,
            string environment = null, string[] dependencies = null) => new()
        {
            Environment = environment ?? name, Reason = "selected", Test = new IntegrationTestDefinition { Name = name, Project = access,
                Seconds = seconds, Windowed = gui, DependsOn = dependencies ?? System.Array.Empty<string>() },
        };
    }
}
