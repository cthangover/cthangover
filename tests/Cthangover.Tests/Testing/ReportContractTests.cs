namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.TestRunner;

    /// <summary>Reports retain leaf failures/skips, round-trip their data and support reruns with prerequisite preparation.</summary>
    public class ReportContractTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "report_contract_" + Guid.NewGuid().ToString("N"));

        public ReportContractTests() => Directory.CreateDirectory(directory);

        [Fact]
        public void JsonRoundTripRetainsEveryExecutionField()
        {
            var result = new TestResult
            {
                Name = "play", Kind = "mod-unit", Passed = false, PassedChecks = 2, FailedChecks = 1, SkippedChecks = 3,
                Duration = TimeSpan.FromSeconds(1.25), ExitCode = 1, Filter = "map/*", ModFilter = "rpg", Environment = "setup", SelectionReason = "selected",
                OverheadSeconds = 0.23, BootSeconds = 0.4, CpuSeconds = 0.8, PeakMemoryMb = 123.5, LogLines = 47,
                DependsOn = new[] { "setup" }, Note = "assertion", FailureKind = "assertion", Core = "ff_battle", Coverage = "2/2", LogFile = "process.log",
                Diagnostics = new() { "failure" }, Artifacts = new() { "actual.png" },
                CandidateLaunches = new[] { "source" },
                Cases = new() { new TestCaseResult { Name = "map/roundtrip", Mod = "rpg", Status = "fail", Message = "expected 1, got 2",
                    StackTrace = "MapTests.cs:20", DurationSeconds = 0.3, Artifacts = new() { "diff.png" }, Log = new() { "step" } } },
            };
            var report = new RunReport { RunId = "roundtrip", Complete = true, Workspace = "workspace", FinishedAt = DateTimeOffset.UtcNow,
                Workspaces = new() { "workspace" }, RetainedWorkspaces = new() { "workspace" },
                Phases = new RunPhases { Copies = 3, LaunchSeconds = 4, Parallelism = 2,
                    LaneBusySeconds = new() { ["shared"] = 5, ["write0"] = 3 }, BuildSeconds = 1, WorkspaceCopySeconds = 2, RestoreSeconds = 3, RestoreCount = 4,
                    ArtifactCollectionSeconds = 5, ReportWritingSeconds = 6, CleanupSeconds = 7 },
                UserDirectories = new() { "private-data" }, Results = new() { result } };
            var restored = JsonSerializer.Deserialize<RunReport>(JsonSerializer.Serialize(report, ReportWriter.JsonOptions), ReportWriter.JsonOptions);
            Assert.Equal(report.RunId, restored.RunId);
            Assert.Equal(report.StartedAt, restored.StartedAt);
            Assert.Equal(report.FinishedAt, restored.FinishedAt);
            Assert.Equal(report.Workspace, restored.Workspace);
            Assert.Equal(report.Workspaces, restored.Workspaces);
            Assert.Equal(report.RetainedWorkspaces, restored.RetainedWorkspaces);
            Assert.Equal(report.UserDirectories, restored.UserDirectories);
            Assert.Equal(report.WallSeconds, restored.WallSeconds);
            Assert.Equal(report.ChildSeconds, restored.ChildSeconds);
            Assert.Equal(JsonSerializer.Serialize(report.Phases, ReportWriter.JsonOptions), JsonSerializer.Serialize(restored.Phases, ReportWriter.JsonOptions));
            var copy = Assert.Single(restored.Results);
            Assert.Equal(JsonSerializer.Serialize(result, ReportWriter.JsonOptions), JsonSerializer.Serialize(copy, ReportWriter.JsonOptions));
            Assert.False(restored.Success);
        }

        [Fact]
        public void PartialCheckpointsWriteOnlyJsonAndKeepUnavailableMetricsNull()
        {
            var report = new RunReport { Results = new() { new TestResult { Name = "driverless" } } };
            ReportWriter.Write(directory, report);
            Assert.True(File.Exists(Path.Combine(directory, "report.json")));
            Assert.False(File.Exists(Path.Combine(directory, "report.html")));
            Assert.False(File.Exists(Path.Combine(directory, "report.junit.xml")));
            var restored = JsonSerializer.Deserialize<RunReport>(File.ReadAllText(Path.Combine(directory, "report.json")), ReportWriter.JsonOptions);
            var result = Assert.Single(restored.Results);
            Assert.Null(result.BootSeconds);
            Assert.Null(result.CpuSeconds);
            Assert.Null(result.PeakMemoryMb);
            Assert.Equal(0, result.LogLines);
            report.Complete = true;
            ReportWriter.Write(directory, report);
            Assert.Contains("Time breakdown", File.ReadAllText(Path.Combine(directory, "report.html")));
            Assert.True(File.Exists(Path.Combine(directory, "report.junit.xml")));
        }

        [Fact]
        public void IncompleteAndBlockedRunsCannotClaimSuccess()
        {
            var report = new RunReport { Results = new() { new TestResult { Name = "probe", Passed = true, PassedChecks = 1 } } };
            Assert.False(report.Success);
            report.Complete = true;
            Assert.True(report.Success);
            report.Results.Add(new TestResult { Name = "dependent", Blocked = true });
            Assert.False(report.Success);
            Assert.Equal("blocked", report.Results[1].Status);
        }

        [Fact]
        public void JUnitRepresentsSkipReasonsAndFailureAfterSuccessfulCases()
        {
            var result = new TestResult { Name = "probe", Note = "process crashed after tests", Cases = new()
            {
                new TestCaseResult { Name = "one", Status = "pass" },
                new TestCaseResult { Name = "two", Status = "skipped", Message = "requires rendering" },
            } };
            var xml = ReportWriter.JUnit(new RunReport { Complete = true, Results = new() { result } });
            Assert.Equal("requires rendering", (string)Assert.Single(xml.Descendants("skipped")).Attribute("message"));
            Assert.Equal("process crashed after tests", (string)Assert.Single(xml.Descendants("error")).Attribute("message"));
            Assert.Equal("3", (string)xml.Element("testsuite").Attribute("tests"));
        }

        [Fact]
        public void BuildFailureAndIncompleteRunAreErrorsInJUnit()
        {
            var report = new RunReport { Build = new TestResult { Name = "build", Note = "compiler error", ExitCode = 1 } };
            var xml = ReportWriter.JUnit(report);
            Assert.Contains(xml.Descendants("failure"), element => (string)element.Attribute("message") == "compiler error");
            Assert.Contains(xml.Descendants("error"), element => (string)element.Attribute("message") == "run is incomplete");
        }

        [Fact]
        public void SavedFailureSelectionRecreatesPrerequisitesAndKeepsCaseFilter()
        {
            var report = new RunReport { Complete = true, Results = new()
            {
                new TestResult { Name = "setup", Passed = true, PassedChecks = 1 },
                new TestResult { Name = "play", Filter = "followers/*", FailedChecks = 1 },
            } };
            ReportWriter.Write(directory, report);
            var catalog = new[]
            {
                new IntegrationTestDefinition { Name = "setup" },
                new IntegrationTestDefinition { Name = "play", DependsOn = new[] { "setup" } },
            };
            var plan = TestPlanner.Build(catalog, RunnerOptions.Parse(new[] { "--failed", Path.Combine(directory, "report.json") }));
            Assert.Equal(new[] { "setup", "play" }, plan.Select(entry => entry.Test.Name));
            Assert.Equal("followers/*", plan[1].Filter);
            Assert.True(File.Exists(Path.Combine(directory, "report.html")));
            Assert.True(File.Exists(Path.Combine(directory, "report.junit.xml")));
        }

        [Fact]
        public void HtmlEscapesTestMessages()
        {
            var html = ReportWriter.Html(new RunReport { Results = new() { new TestResult { Name = "<script>", Note = "<img src=x>" } } });
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;img src=x&gt;", html);
        }

        [Fact]
        public void FailedIndividualAndAggregateModSelectionsKeepTheirFilters()
        {
            var report = new RunReport { Complete = true, Results = new()
            {
                new TestResult { Name = "mod_units:rpg", Filter = "followers/*", FailedChecks = 1 },
                new TestResult { Name = "mod_units", Filter = "map/*", ModFilter = "rpg", FailedChecks = 1 },
            } };
            ReportWriter.Write(directory, report);
            var catalog = new[]
            {
                new IntegrationTestDefinition { Name = "mod_units:rpg", Kind = "mod-unit", Tags = new[] { "individual" } },
                new IntegrationTestDefinition { Name = "mod_units", Kind = "mod-unit" },
            };
            var plan = TestPlanner.Build(catalog, RunnerOptions.Parse(new[] { "--failed", Path.Combine(directory, "report.json") }));
            Assert.Equal("followers/*", plan.Single(entry => entry.Test.Name == "mod_units:rpg").Filter);
            Assert.Equal("map/*", plan.Single(entry => entry.Test.Name == "mod_units").Filter);
            Assert.Equal("rpg", plan.Single(entry => entry.Test.Name == "mod_units").ModFilter);
        }

        public void Dispose() => Directory.Delete(directory, true);
    }
}
