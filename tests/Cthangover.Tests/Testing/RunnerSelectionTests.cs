namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.TestRunner;

    /// <summary>Selection preserves setup, isolates components and never turns an empty or invalid request green.</summary>
    [Collection("runner-infrastructure")]
    public class RunnerSelectionTests : System.IDisposable
    {
        private readonly string failureReport = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "selection_report_" + System.Guid.NewGuid().ToString("N") + ".json");

        [Fact]
        public void RetentionEnvironmentAndCliHaveDocumentedDefaultsAndPrecedence()
        {
            var names = new[] { "CTHANGOVER_TEST_RETENTION_DAYS", "CTHANGOVER_TEST_RETENTION_COUNT", "CTHANGOVER_TEST_RETENTION_GIB" };
            var previous = names.Select(Environment.GetEnvironmentVariable).ToArray();
            try
            {
                foreach (var name in names) Environment.SetEnvironmentVariable(name, null);
                var defaults = RunnerOptions.Parse(Array.Empty<string>());
                Assert.Equal(3, defaults.RetentionCount); Assert.Equal(3, defaults.RetentionDays);
                Assert.Equal(20L * 1024 * 1024 * 1024, defaults.RetentionBytes);
                Environment.SetEnvironmentVariable(names[0], "2.5");
                Environment.SetEnvironmentVariable(names[1], "7");
                Environment.SetEnvironmentVariable(names[2], "0.5");
                var environment = RunnerOptions.Parse(Array.Empty<string>());
                Assert.Equal(2.5, environment.RetentionDays); Assert.Equal(7, environment.RetentionCount);
                Assert.Equal(512L * 1024 * 1024, environment.RetentionBytes);
                var cli = RunnerOptions.Parse(new[] { "--retention-days", "1", "--retention-count", "2", "--retention-gib", "3" });
                Assert.Equal(1, cli.RetentionDays); Assert.Equal(2, cli.RetentionCount);
                Assert.Equal(3L * 1024 * 1024 * 1024, cli.RetentionBytes);
            }
            finally { for (var i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], previous[i]); }
        }

        [Theory]
        [InlineData("--retention-days", "NaN")]
        [InlineData("--retention-days", "-1")]
        [InlineData("--retention-count", "-1")]
        [InlineData("--retention-count", "2.5")]
        [InlineData("--retention-gib", "Infinity")]
        [InlineData("--retention-gib", "1e30")]
        public void InvalidRetentionValuesAreRejected(string option, string value)
            => Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(new[] { option, value }));
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

        [Fact]
        public void OneJobKeepsTheOriginalPlanAndOrder()
        {
            var original = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--test", "play", "--test", "other" }));
            var sequential = TestPlanner.Build(Catalog(), RunnerOptions.Parse(new[] { "--test", "play", "--test", "other", "--jobs", "1" }));
            Assert.Equal(JsonSerializer.Serialize(original, ReportWriter.JsonOptions), JsonSerializer.Serialize(sequential, ReportWriter.JsonOptions));
            Assert.Equal(new[] { "setup", "play", "other" }, sequential.Select(entry => entry.Test.Name));
        }

        [Theory]
        [InlineData("--jobs", "0")]
        [InlineData("--gui-jobs", "-1")]
        [InlineData("--write-jobs", "no")]
        public void ConcurrencyLimitsMustBePositive(string option, string value)
            => Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(new[] { option, value }));

        [Fact]
        public void UnselectedDependentCannotAuthorizeOverlappingOutputDirectories()
        {
            var catalog = Catalog();
            catalog.Add(new IntegrationTestDefinition { Name = "unselected", DependsOn = new[] { "play", "other" } });
            foreach (var name in new[] { "play", "other" })
            {
                var test = catalog.Single(entry => entry.Name == name);
                test.Project = "output";
                test.Args = new[] { "--shots=res://tests/artifacts/shared" };
            }
            ProjectAccess.ValidateOutputs(catalog);
            Assert.Throws<InvalidOperationException>(() => TestPlanner.Build(catalog,
                RunnerOptions.Parse(new[] { "--test", "play", "--test", "other" })));
        }

        [Fact]
        public void SharedIntegrityFailureRepeatsCandidatesWithTheirFiltersAndPrerequisites()
        {
            var catalog = Catalog();
            catalog.Single(test => test.Name == "mod_units:rpg").DependsOn = new[] { "setup" };
            var report = new RunReport { Results = new()
            {
                new TestResult { Name = "mod_units:rpg", Passed = true, PassedChecks = 1, Filter = "followers/*" },
                new TestResult { Name = "shared-project-integrity", Kind = "infrastructure", CandidateLaunches = new[] { "mod_units:rpg" } },
            } };
            System.IO.File.WriteAllText(failureReport, JsonSerializer.Serialize(report, ReportWriter.JsonOptions));
            var plan = TestPlanner.Build(catalog, RunnerOptions.Parse(new[] { "--failed", failureReport }));
            Assert.Equal(new[] { "setup", "mod_units:rpg" }, plan.Select(entry => entry.Test.Name));
            Assert.Null(plan[0].Filter);
            Assert.Equal("followers/*", plan[1].Filter);
        }

        public void Dispose() => System.IO.File.Delete(failureReport);
    }
}
