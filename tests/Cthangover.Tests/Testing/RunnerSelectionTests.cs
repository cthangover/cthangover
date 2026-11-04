namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.TestRunner;

    /// <summary>Selection preserves setup, isolates components and never turns an empty or invalid request green.</summary>
    public class RunnerSelectionTests
    {
        private static List<IntegrationTestDefinition> Catalog() => new()
        {
            new() { Name = "unit", Kind = "unit", Profiles = new[] { "full", "fast" }, Owners = new[] { "core" } },
            new() { Name = "setup", Owners = new[] { "tools" }, Profiles = new[] { "full" } },
            new() { Name = "play", Owners = new[] { "rpg" }, Profiles = new[] { "full" }, DependsOn = new[] { "setup" }, Renderer = "isometric" },
            new() { Name = "other", Owners = new[] { "items_base" }, Profiles = new[] { "full" } },
            new() { Name = "mod_units:rpg", Kind = "mod-unit", Owners = new[] { "rpg" }, Profiles = new[] { "full", "fast" }, Tags = new[] { "individual" } },
        };

        [Fact]
        public void SingleDependentAutomaticallyGetsSetupInTheSameEnvironment()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--test", "play" }));
            Assert.Equal(new[] { "setup", "play" }, plan.Select(e => e.Test.Name));
            Assert.Equal(plan[0].Environment, plan[1].Environment);
            Assert.Contains("play", plan[0].Reason);
        }

        [Fact]
        public void OwnerAndRendererFiltersCannotDropNecessarySetup()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--mod", "rpg", "--renderer", "isometric" }));
            Assert.Equal(new[] { "setup", "play" }, plan.Select(e => e.Test.Name));
        }

        [Fact]
        public void ACaseFilterTargetsOnlyTheRequestedMod()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--mod", "rpg", "--filter", "followers/*" }));
            Assert.Equal("mod_units:rpg", Assert.Single(plan).Test.Name);
            Assert.Equal("followers/*", plan[0].Filter);
        }

        [Fact]
        public void RepeatedGlobsAreAUnionWithoutDuplicatePrerequisites()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--test", "p*", "--test", "setup", "--test", "other" }));
            Assert.Equal(3, plan.Count);
            Assert.Equal(1, plan.Count(e => e.Test.Name == "setup"));
            Assert.NotEqual(plan.Single(e => e.Test.Name == "play").Environment, plan.Single(e => e.Test.Name == "other").Environment);
        }

        [Theory]
        [InlineData("--test", "missing")]
        [InlineData("--profile", "missing")]
        [InlineData("--mod", "missing")]
        [InlineData("--renderer", "missing")]
        public void UnknownOrEmptySelectionIsAnError(string option, string value)
            => Assert.Throws<ArgumentException>(() => TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { option, value })));

        [Fact]
        public void FastProfileExcludesTheRepositoryCompileGuard()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--profile", "fast" }));
            Assert.Equal("unit", Assert.Single(plan).Test.Name);
            Assert.Contains("!~ToolsModCompileTests", plan[0].Filter);
        }

        [Fact]
        public void CyclesAreRejectedBeforeExecution()
        {
            var catalog = Catalog();
            catalog.Single(t => t.Name == "setup").DependsOn = new[] { "play" };
            Assert.Throws<InvalidOperationException>(() => TestPlanner.Build(catalog, RunnerOptions.Parse(new[] { "--test", "play" })));
        }

        [Fact]
        public void AValueCannotSilentlyConsumeTheNextOption()
            => Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(new[] { "--test", "--list" }));

        [Fact]
        public void AnUnselectedDependentDoesNotMakeSelectedTestsShareState()
        {
            var catalog = Catalog();
            catalog.Add(new IntegrationTestDefinition { Name = "unselected", DependsOn = new[] { "play", "other" } });
            var plan = TestPlanner.Build(catalog, RunnerOptions.Parse(new[] { "--test", "play", "--test", "other" }));
            Assert.NotEqual(plan.Single(e => e.Test.Name == "play").Environment, plan.Single(e => e.Test.Name == "other").Environment);
        }

        [Fact]
        public void SelectedCaseFilterDoesNotFilterItsPreparation()
        {
            var catalog = Catalog();
            catalog.Single(test => test.Name == "mod_units:rpg").DependsOn = new[] { "setup" };
            var plan = TestPlanner.Build(catalog, RunnerOptions.Parse(new[] { "--mod", "rpg", "--filter", "followers/*" }));
            Assert.Null(plan[0].Filter);
            Assert.Equal("followers/*", plan[1].Filter);
        }

        [Fact]
        public void ExecutionPlanSurvivesJsonRoundTrip()
        {
            var plan = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--mod", "rpg", "--filter", "followers/*" }));
            var json = JsonSerializer.Serialize(plan, ReportWriter.JsonOptions);
            var copy = JsonSerializer.Deserialize<List<TestPlanEntry>>(json, ReportWriter.JsonOptions);
            Assert.Equal(json, JsonSerializer.Serialize(copy, ReportWriter.JsonOptions));
        }
    }
}
