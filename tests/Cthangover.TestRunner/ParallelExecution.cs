namespace Cthangover.TestRunner
{
    using System.Collections.Concurrent;
    using System.Diagnostics;

    /// <summary>Executes scheduled components in physical lanes, with private data homes on the immutable shared copy.</summary>
    internal sealed class ParallelExecution : IParallelExecutor, IDisposable
    {
        private readonly string root;
        private readonly string runId;
        private readonly string artifacts;
        private readonly UnitManifest unit;
        private readonly IReadOnlyList<TestPlanEntry> plan;
        private readonly RunnerOptions options;
        private readonly RunReport report;
        private readonly ConcurrentDictionary<int, TestWorkspace> lanes = new();
        private readonly ConcurrentDictionary<int, string> copyRoots = new();
        private readonly ConcurrentDictionary<string, TestWorkspace> environments = new();
        private readonly ConcurrentDictionary<int, double> copySeconds = new();
        private readonly ConcurrentDictionary<string, (double Restore, double Collection)> timings = new();
        private readonly HashSet<int> failedLanes = new();
        private readonly List<string> sharedLaunches = new();
        private readonly Dictionary<string, int> indices;
        private bool finished;
        private long launchStarted;
        private long launchFinished;
        private bool disposed;

        internal ParallelExecution(string root, string runId, string artifacts, UnitManifest unit,
            IReadOnlyList<TestPlanEntry> plan, RunnerOptions options, RunReport report)
        {
            this.root = root;
            this.runId = runId;
            this.artifacts = artifacts;
            this.unit = unit;
            this.plan = plan;
            this.options = options;
            this.report = report;
            indices = plan.Select((entry, index) => (entry.Test.Name, index)).ToDictionary(pair => pair.Name, pair => pair.index);
        }

        internal void Run()
        {
            try
            {
                ParallelScheduler.Run(plan, options, this, RunnerLifetime.Token);
                if (lanes.TryGetValue(-1, out var shared))
                {
                    var changed = shared.VerifyUnchanged();
                    if (changed.Count > 0)
                    {
                        failedLanes.Add(-1);
                        var result = new TestResult { Name = "shared-project-integrity", Kind = "infrastructure", ExitCode = -1,
                            FailureKind = "infrastructure", Note = "shared project changed: " + string.Join(", ", changed),
                            CandidateLaunches = sharedLaunches.ToArray(),
                            Diagnostics = new() { "Candidate launches: " + string.Join(", ", sharedLaunches) } };
                        result.Artifacts.AddRange(shared.CaptureChanges(Path.Combine(artifacts, "shared-project-changes")));
                        report.Results.Add(result);
                        RunnerConsole.WriteLine(result.Note + "; " + result.Diagnostics[0], result.Name);
                        ReportWriter.Write(artifacts, report, options.Json);
                    }
                }
                finished = true;
            }
            finally
            {
                var end = finished && launchFinished != 0 ? launchFinished : Stopwatch.GetTimestamp();
                report.Phases.LaunchSeconds = launchStarted == 0 ? 0 : Stopwatch.GetElapsedTime(launchStarted, end).TotalSeconds;
                report.Phases.Parallelism = report.Phases.LaunchSeconds > 0 ? report.ChildSeconds / report.Phases.LaunchSeconds : 0;
                UpdateCopies();
            }
        }

        /// <inheritdoc />
        public void PrepareLane(int lane)
        {
            var started = Stopwatch.GetTimestamp();
            RunnerConsole.WriteLine("Подготовка копии проекта...", LaneName(lane));
            var copyId = runId + "_" + LaneName(lane);
            copyRoots[lane] = Path.Combine(Path.GetTempPath(), "cthangover-tests", copyId, "project");
            try
            {
                var workspace = new TestWorkspace(root, copyId, options.FreshCache, runId, artifacts) { Keep = true };
                lanes[lane] = workspace;
                if (lane == -1) workspace.ConfigureShared();
            }
            finally { copySeconds[lane] = Stopwatch.GetElapsedTime(started).TotalSeconds; }
            RunnerConsole.WriteLine("Копия готова", LaneName(lane));
        }

        /// <inheritdoc />
        public TestResult Execute(TestPlanEntry entry, int lane, bool first, CancellationToken cancellationToken)
        {
            var restoreSeconds = 0.0;
            var collectionSeconds = 0.0;
            var directory = Path.Combine(artifacts, "tests", TestWorkspace.Key(entry.Test.Name));
            Directory.CreateDirectory(directory);
            TestWorkspace workspace = null;
            TestResult result;
            Interlocked.CompareExchange(ref launchStarted, Stopwatch.GetTimestamp(), 0);
            RunnerConsole.WriteLine($"[{indices[entry.Test.Name] + 1}/{plan.Count}] {entry.Reason}...", entry.Test.Name);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (first)
                {
                    workspace = lanes[lane];
                    if (lane == -1) workspace = workspace.SharedComponent(entry.Environment);
                    else
                    {
                        var before = workspace.RestoreSeconds;
                        try { workspace.Begin(entry.Environment, restore: true); }
                        finally { restoreSeconds = workspace.RestoreSeconds - before; }
                    }
                    environments[entry.Environment] = workspace;
                }
                else workspace = environments[entry.Environment];
                result = entry.Test.Kind == "unit" ? Program.RunUnit(workspace, unit, entry, directory, entry.Test.Name, cancellationToken)
                    : Program.RunIntegration(workspace, GodotLocator.Locate(options.Godot), entry, directory, entry.Test.Name, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                result = new TestResult { Name = entry.Test.Name, Kind = entry.Test.Kind, ExitCode = -1,
                    FailureKind = "infrastructure", Note = "infrastructure: " + error.Message, Diagnostics = new() { error.ToString() } };
            }
            if (workspace?.UserDirectory != null)
            {
                var started = Stopwatch.GetTimestamp();
                try { ArtifactCollector.Collect(workspace, directory, result, entry.Test, shared: lane == -1); }
                catch (Exception error) { result.Passed = false; result.FailureKind ??= "artifacts"; result.Note += "; artifact collection: " + error.Message; }
                finally { collectionSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds; }
            }
            result.OverheadSeconds = restoreSeconds + collectionSeconds;
            timings[entry.Test.Name] = (restoreSeconds, collectionSeconds);
            Interlocked.Exchange(ref launchFinished, Stopwatch.GetTimestamp());
            return result;
        }

        /// <inheritdoc />
        public void Completed(TestPlanEntry entry, TestResult result, int lane)
        {
            report.Results[indices[entry.Test.Name]] = result;
            if (!result.Passed) failedLanes.Add(lane);
            if (lane == -1 && !result.Blocked) sharedLaunches.Add(entry.Test.Name);
            if (timings.TryGetValue(entry.Test.Name, out var timing)) report.Phases.ArtifactCollectionSeconds += timing.Collection;
            var name = LaneName(lane);
            report.Phases.LaneBusySeconds[name] = report.Phases.LaneBusySeconds.GetValueOrDefault(name) + result.DurationSeconds;
            UpdateCopies();
            RunnerConsole.WriteLine($"{result.Status.ToUpperInvariant()} {result.PassedChecks}/{result.PassedChecks + result.FailedChecks} skipped={result.SkippedChecks} {result.DurationSeconds:0.0}s {result.Note}", entry.Test.Name);
            ReportWriter.Write(artifacts, report, options.Json);
        }

        private void UpdateCopies()
        {
            report.Workspaces = copyRoots.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
            report.Workspace = string.Join(", ", report.Workspaces);
            report.Phases.Copies = copyRoots.Count;
            // Copies overlap, including early launches. This is preparation wall, not the sum of concurrent copy work.
            report.Phases.WorkspaceCopySeconds = copySeconds.Values.DefaultIfEmpty(0).Max();
            report.Phases.RestoreSeconds = lanes.Values.Sum(workspace => workspace.RestoreSeconds);
            report.Phases.RestoreCount = lanes.Values.Sum(workspace => workspace.RestoreCount);
            report.UserDirectories = environments.Values.Select(workspace => workspace.UserDirectory)
                .Where(path => path != null).Concat(report.UserDirectories).Distinct().ToList();
        }

        private static string LaneName(int lane) => lane < 0 ? "shared" : "write" + lane;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var started = Stopwatch.GetTimestamp();
            foreach (var pair in lanes)
            {
                var workspace = pair.Value;
                workspace.Keep = !finished || options.KeepWorkspace || failedLanes.Contains(pair.Key);
                try { workspace.Dispose(); }
                catch (Exception error)
                {
                    workspace.Keep = true;
                    report.Results.Add(new TestResult { Name = "cleanup:" + LaneName(pair.Key), Kind = "infrastructure", ExitCode = -1,
                        FailureKind = "cleanup", Note = error.Message, Diagnostics = new() { error.ToString() } });
                }
            }
            report.WorkspaceRetained = lanes.Values.Any(workspace => workspace.Keep);
            report.RetainedWorkspaces = lanes.Values.Where(workspace => workspace.Keep).Select(workspace => workspace.Root).ToList();
            foreach (var copy in copyRoots)
                if (!lanes.ContainsKey(copy.Key) && Directory.Exists(copy.Value)) report.RetainedWorkspaces.Add(copy.Value);
            report.WorkspaceRetained |= report.RetainedWorkspaces.Count > 0;
            if (!report.WorkspaceRetained && options.FreshCache)
            {
                try
                {
                    var cacheRoot = Path.GetDirectoryName(CompilationCachePolicy.Resolve(Path.GetTempPath(), runId, true));
                    if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
                    var runRoot = Path.GetDirectoryName(cacheRoot);
                    if (Directory.Exists(runRoot) && !Directory.EnumerateFileSystemEntries(runRoot).Any()) Directory.Delete(runRoot);
                }
                catch (Exception error)
                {
                    report.Results.Add(new TestResult { Name = "cleanup:cache", Kind = "infrastructure", ExitCode = -1,
                        FailureKind = "cleanup", Note = error.Message, Diagnostics = new() { error.ToString() } });
                }
            }
            report.Phases.CleanupSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
        }
    }
}
