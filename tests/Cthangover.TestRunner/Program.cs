namespace Cthangover.TestRunner
{
    using System.Text;
    using System.Text.Json;
    using System.Diagnostics;

    /// <summary>Plans first, builds once, executes dependency groups in a private workspace and persists every outcome.</summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            using var lifetime = new RunnerLifetime();
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try { return Run(RunnerOptions.Parse(args)); }
            catch (Exception error) { Console.Error.WriteLine("test runner error: " + error.Message); return 2; }
        }

        private static int Run(RunnerOptions options)
        {
            var startedAt = DateTimeOffset.UtcNow;
            if (options.ShowHelp) { Console.WriteLine(RunnerOptions.HelpText); return 0; }
            var root = ProjectLocator.Locate();
            var manifest = TestManifest.LoadProject(root);
            var catalog = TestCatalog.Load(root, manifest);
            if (options.List || options.Catalog != null)
            {
                var listed = catalog.Where(t => (options.Mod == null || t.Owners.Contains(options.Mod))
                    && (options.Profile == null || t.Profiles.Contains(options.Profile))
                    && (options.Kind == null || t.Kind == options.Kind) && (options.Tag == null || t.Tags.Contains(options.Tag))
                    && (options.Game == null || t.Game == options.Game) && (options.Renderer == null || t.Renderer == options.Renderer)
                    && (options.Tests.Count == 0 || options.Tests.Any(p => NamePattern.Matches(t.Name, p)))).ToList();
                if (listed.Count == 0) throw new ArgumentException("catalogue selection is empty");
                foreach (var test in listed)
                    Console.WriteLine($"{test.Name,-38} {test.Kind,-12} owners={string.Join(',', test.Owners)} profiles={string.Join(',', test.Profiles)} {(test.Windowed ? "windowed" : "headless")} {test.ManualReason}");
                if (options.Catalog != null) TestCatalog.Write(options.Catalog, listed);
                return 0;
            }
            var plan = options.Switcher == null ? TestPlanner.Build(catalog, options) : SmokePlan(options);
            if (options.Plan)
            {
                foreach (var entry in plan)
                    Console.WriteLine($"{entry.Test.Name,-38} {entry.Reason}; env={entry.Environment}; depends={string.Join(',', entry.Test.DependsOn)}; filter={entry.Filter}");
                return 0;
            }
            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var artifacts = Path.Combine(Path.GetFullPath(options.Artifacts ?? Path.Combine(root, "tests", "artifacts", "runs")), runId);
            Directory.CreateDirectory(artifacts);
            File.WriteAllText(Path.Combine(artifacts, "plan.json"), JsonSerializer.Serialize(plan, ReportWriter.JsonOptions));
            TestCatalog.Write(Path.Combine(artifacts, "catalog.json"), catalog);
            var report = new RunReport { RunId = runId, StartedAt = startedAt, Results = plan.Select(Pending).ToList() };
            ReportWriter.Write(artifacts, report, options.Json);
            Console.WriteLine("Запуск " + runId + "; отчёты: " + artifacts);
            TestWorkspace workspace = null;
            ParallelExecution parallel = null;
            try
            {
                WorkspaceRetention.Prune(Path.GetTempPath(), options.RetentionCount, options.RetentionDays, options.RetentionBytes);
                CompilationCachePolicy.Prune(CompilationCachePolicy.Resolve(Path.GetTempPath(), runId, false), DateTime.UtcNow);
                var unit = plan.Any(e => e.Test.Kind == "unit");
                if (!options.NoBuild || unit)
                {
                    Console.WriteLine("Сборка проекта и тестов...");
                    var project = unit ? manifest.Unit.Project : "Cthangover.csproj";
                    var build = ProcessRunner.Run("dotnet", new[] { "build", project, "--nologo", "-v", "minimal" }, root, 300, Path.Combine(artifacts, "build.log"),
                        launchName: options.Jobs > 1 ? "build" : null, cancellationToken: RunnerLifetime.Token);
                    report.Build = new TestResult { Name = "build", Kind = "build", Passed = build.ExitCode == 0 && !build.TimedOut,
                        ExitCode = build.ExitCode, TimedOut = build.TimedOut, Duration = build.Duration, LogFile = build.LogFile,
                        BootSeconds = build.BootSeconds, CpuSeconds = build.CpuSeconds, PeakMemoryMb = build.PeakMemoryMb, LogLines = build.LogLines,
                        Note = build.TimedOut ? "build timeout" : build.ExitCode == 0 ? null : "build failed",
                        FailureKind = build.TimedOut ? "timeout" : build.ExitCode == 0 ? null : "compilation",
                        Diagnostics = build.ExitCode == 0 ? new() : TestLogs.Highlights(build.Output) };
                    report.Phases.BuildSeconds = build.Duration.TotalSeconds;
                    if (!report.Build.Passed) return Finish(artifacts, report, options, workspace);
                }
                if (options.Jobs > 1)
                {
                    parallel = new ParallelExecution(root, runId, artifacts, manifest.Unit, plan, options, report);
                    parallel.Run();
                    return Finish(artifacts, report, options, null, parallel);
                }
                Console.WriteLine("Подготовка отдельной копии проекта...");
                var copyStarted = Stopwatch.GetTimestamp();
                try { workspace = new TestWorkspace(root, runId, options.FreshCache, artifactsDirectory: artifacts); }
                finally { report.Phases.WorkspaceCopySeconds = Stopwatch.GetElapsedTime(copyStarted).TotalSeconds; }
                report.Workspace = workspace.Root;
                report.Workspaces.Add(workspace.Root);
                report.Phases.Copies = 1;
                workspace.Keep = true; // An interrupted run must leave its diagnostics and mutable copy available.
                var completed = new Dictionary<string, TestResult>(StringComparer.Ordinal);
                string component = null;
                var launchStarted = Stopwatch.GetTimestamp();
                for (var i = 0; i < plan.Count; i++)
                {
                    RunnerLifetime.Token.ThrowIfCancellationRequested();
                    var entry = plan[i];
                    TestResult result;
                    var restoreSeconds = 0.0;
                    var blocked = entry.Test.DependsOn.Where(name => !completed.TryGetValue(name, out var prerequisite) || !prerequisite.Passed).ToArray();
                    if (blocked.Length > 0)
                    {
                        result = Pending(entry);
                        result.Note = "blocked by unsuccessful prerequisite(s): " + string.Join(", ", blocked);
                        result.FailureKind = "dependency";
                    }
                    else
                    {
                        if (component != entry.Environment)
                        {
                            var restoredBefore = workspace.RestoreSeconds;
                            workspace.Begin(entry.Environment, restore: true);
                            restoreSeconds = workspace.RestoreSeconds - restoredBefore;
                            report.Phases.RestoreSeconds = workspace.RestoreSeconds;
                            report.Phases.RestoreCount = workspace.RestoreCount;
                            component = entry.Environment;
                            report.UserDirectories.Add(workspace.UserDirectory);
                            ReportWriter.Write(artifacts, report, options.Json);
                        }
                        var directory = Path.Combine(artifacts, "tests", TestWorkspace.Key(entry.Test.Name));
                        Directory.CreateDirectory(directory);
                        Console.WriteLine($"[{i + 1}/{plan.Count}] {entry.Test.Name} ({entry.Reason})...");
                        try
                        {
                            result = entry.Test.Kind == "unit" ? RunUnit(workspace, manifest.Unit, entry, directory)
                                : RunIntegration(workspace, GodotLocator.Locate(options.Godot), entry, directory);
                        }
                        catch (Exception error) when (error is not OperationCanceledException)
                        {
                            result = new TestResult { Name = entry.Test.Name, Kind = entry.Test.Kind, Note = "infrastructure: " + error.Message,
                                FailureKind = "infrastructure",
                                ExitCode = -1, Diagnostics = new() { error.ToString() } };
                        }
                        var collectStarted = Stopwatch.GetTimestamp();
                        try { ArtifactCollector.Collect(workspace, directory, result); }
                        catch (Exception error) { result.Passed = false; result.FailureKind ??= "artifacts"; result.Note += "; artifact collection: " + error.Message; }
                        finally
                        {
                            var collectionSeconds = Stopwatch.GetElapsedTime(collectStarted).TotalSeconds;
                            report.Phases.ArtifactCollectionSeconds += collectionSeconds;
                            result.OverheadSeconds = restoreSeconds + collectionSeconds;
                        }
                    }
                    result.Environment = entry.Environment;
                    result.Filter = entry.Filter;
                    result.ModFilter = entry.ModFilter;
                    result.SelectionReason = entry.Reason;
                    result.DependsOn = entry.Test.DependsOn;
                    completed[entry.Test.Name] = result;
                    report.Results[i] = result;
                    Console.WriteLine($"  {result.Status.ToUpperInvariant()} {result.PassedChecks}/{result.PassedChecks + result.FailedChecks} skipped={result.SkippedChecks} {result.DurationSeconds:0.0}s {result.Note}");
                    ReportWriter.Write(artifacts, report, options.Json);
                }
                report.Phases.LaunchSeconds = Stopwatch.GetElapsedTime(launchStarted).TotalSeconds;
                report.Phases.Parallelism = report.ChildSeconds / report.Phases.LaunchSeconds;
                report.Phases.LaneBusySeconds["sequential"] = report.ChildSeconds;
                workspace.Keep = options.KeepWorkspace || report.Results.Any(result => !result.Passed);
                return Finish(artifacts, report, options, workspace);
            }
            catch (Exception error)
            {
                RunnerLifetime.Cancel();
                report.Results.Add(new TestResult { Name = "infrastructure", Kind = "infrastructure", ExitCode = -1,
                    FailureKind = "infrastructure",
                    Note = error.Message, Diagnostics = new() { error.ToString() } });
                return Finish(artifacts, report, options, workspace, parallel, complete: false);
            }
        }

        internal static TestResult Pending(TestPlanEntry entry) => new()
        {
            Name = entry.Test.Name, Kind = entry.Test.Kind, Blocked = true, ExitCode = -1, Note = "not started",
            Environment = entry.Environment, Filter = entry.Filter, SelectionReason = entry.Reason, DependsOn = entry.Test.DependsOn,
            ModFilter = entry.ModFilter,
            FailureKind = "not-started",
        };

        private static int Finish(string directory, RunReport report, RunnerOptions options, TestWorkspace workspace,
            ParallelExecution parallel = null, bool complete = true)
        {
            parallel?.Dispose();
            if (workspace != null)
            {
                report.Phases.RestoreSeconds = workspace.RestoreSeconds;
                report.Phases.RestoreCount = workspace.RestoreCount;
                var cleanupStarted = Stopwatch.GetTimestamp();
                try { workspace.Dispose(); }
                catch (Exception error)
                {
                    workspace.Keep = true;
                    report.Results.Add(new TestResult { Name = "cleanup", Kind = "infrastructure", ExitCode = -1,
                        FailureKind = "cleanup",
                        Note = "workspace cleanup failed: " + error.Message, Diagnostics = new() { error.ToString() } });
                }
                finally { report.Phases.CleanupSeconds = Stopwatch.GetElapsedTime(cleanupStarted).TotalSeconds; }
                report.WorkspaceRetained = workspace.Keep;
                if (workspace.Keep) report.RetainedWorkspaces.Add(workspace.Root);
            }
            report.Complete = complete;
            report.FinishedAt = DateTimeOffset.UtcNow;
            ReportWriter.Write(directory, report, options.Json);
            WorkspaceRetention.Prune(Path.GetTempPath(), options.RetentionCount, options.RetentionDays, options.RetentionBytes);
            report.RetainedWorkspaces.RemoveAll(path => !Directory.Exists(path));
            report.WorkspaceRetained = report.RetainedWorkspaces.Count > 0;
            ReportWriter.Write(directory, report, options.Json);
            SummaryPrinter.Print(report.Build == null ? report.Results : report.Results.Prepend(report.Build).ToList());
            SummaryPrinter.PrintTiming(report);
            Console.WriteLine("JSON / HTML / JUnit: " + directory);
            if (report.WorkspaceRetained) Console.WriteLine("Окружение сохранено: " + string.Join(", ", report.RetainedWorkspaces));
            return report.Success ? 0 : 1;
        }

        internal static TestResult RunUnit(TestWorkspace workspace, UnitManifest unit, TestPlanEntry entry, string directory,
            string launchName = null, CancellationToken cancellationToken = default)
        {
            var assembly = Path.Combine(workspace.Root, Path.GetDirectoryName(unit.Project), "bin", "Debug", "net10.0", Path.GetFileNameWithoutExtension(unit.Project) + ".dll");
            if (!File.Exists(assembly)) throw new FileNotFoundException("built unit-test assembly not found", assembly);
            var args = new List<string> { "vstest", assembly, "--logger:trx;LogFileName=unit.trx", "--logger:console;verbosity=normal", "--ResultsDirectory:" + directory };
            var settings = launchName == null ? null : UnitRunSettings.Write(directory);
            if (settings != null) args.Add("--Settings:" + settings);
            if (entry.Filter != null) args.Add("--TestCaseFilter:" + entry.Filter);
            var working = Path.Combine(launchName == null ? workspace.Root : workspace.TemporaryDirectory, "unit-work");
            Directory.CreateDirectory(working);
            var run = ProcessRunner.Run("dotnet", args, working, entry.Test.TimeoutSeconds, Path.Combine(directory, "process.log"), workspace.EnvironmentVariables(), launchName, cancellationToken);
            var trx = TrxParser.Read(Path.Combine(directory, "unit.trx"));
            var artifacts = new List<string>();
            if (File.Exists(Path.Combine(directory, "unit.trx"))) artifacts.Add(Path.Combine(directory, "unit.trx"));
            if (settings != null) artifacts.Add(settings);
            return new TestResult
            {
                Name = entry.Test.Name, Kind = "unit", Passed = run.ExitCode == 0 && !run.TimedOut && trx.Failed == 0 && trx.Problems.Count == 0,
                ExitCode = run.ExitCode, TimedOut = run.TimedOut, Duration = run.Duration, LogFile = run.LogFile,
                BootSeconds = run.BootSeconds, CpuSeconds = run.CpuSeconds, PeakMemoryMb = run.PeakMemoryMb, LogLines = run.LogLines,
                PassedChecks = trx.Passed, FailedChecks = trx.Failed, SkippedChecks = trx.Skipped, Cases = trx.Cases,
                Note = string.Join("; ", trx.Problems), Diagnostics = run.ExitCode == 0 && trx.Problems.Count == 0 ? new() : TestLogs.Highlights(run.Output),
                FailureKind = run.TimedOut ? "timeout" : trx.Problems.Count > 0 ? "contract" : trx.Failed > 0 ? "assertion" : run.ExitCode == 0 ? null : "process",
                Artifacts = artifacts,
            };
        }

        internal static TestResult RunIntegration(TestWorkspace workspace, string godot, TestPlanEntry entry, string directory,
            string launchName = null, CancellationToken cancellationToken = default)
        {
            var test = entry.Test;
            var problem = ScenePathValidator.Check(workspace.Root, test.Scene);
            if (problem != null) throw new InvalidOperationException(problem);
            var args = new List<string>();
            if (!test.Windowed) args.Add("--headless");
            args.AddRange(new[] { "--path", workspace.Root, test.Scene });
            var userArgs = new List<string>(test.Args ?? Array.Empty<string>());
            if (!userArgs.Contains("--")) userArgs.Add("--");
            if (!userArgs.Any(a => a.StartsWith("--game=", StringComparison.Ordinal) || a == "--all-mods" || a == "--frontend")) userArgs.Add("--all-mods");
            if (!userArgs.Contains("--hot-reload")) userArgs.Add("--no-hot-reload");
            if (entry.ModFilter != null) userArgs.Add("--mod=" + entry.ModFilter);
            if (entry.Filter != null) userArgs.Add("--test-filter=" + entry.Filter);
            args.AddRange(userArgs);
            var modReport = Path.Combine(workspace.UserDirectory, "mod_tests", "last-report.json");
            if (test.Kind == "mod-unit" && File.Exists(modReport)) File.Delete(modReport);
            var run = ProcessRunner.Run(godot, args, workspace.Root, test.TimeoutSeconds, Path.Combine(directory, "process.log"), workspace.EnvironmentVariables(), launchName, cancellationToken);
            var result = ResultParser.Evaluate(test.Name, test.Kind, run, test.Result);
            if (run.Output.Any(IsDataHomeMismatch))
            {
                result.Passed = false;
                result.FailureKind = "infrastructure";
                result.Note += "; private OS data home was not honored; retry with --jobs 1";
            }
            if (test.Kind == "mod-unit")
            {
                try { ModReportReader.Import(modReport, result); }
                catch (Exception error) { result.Passed = false; result.FailureKind ??= "contract"; result.Note += "; " + error.Message; }
            }
            return result;
        }

        private static bool IsDataHomeMismatch(string line) => line.Contains("Test user data isolation mismatch", StringComparison.OrdinalIgnoreCase);

        private static List<TestPlanEntry> SmokePlan(RunnerOptions options)
        {
            var args = new List<string> { "--", "--all-mods", "--timeout=" + options.TimeoutSeconds };
            var isScene = options.Switcher.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase);
            if (!isScene) args.Add("--scene=" + options.Switcher);
            if (options.Choices != null) args.Add("--choices=" + options.Choices);
            return new() { new TestPlanEntry { Environment = "switcher", Reason = "smoke scene", Test = new IntegrationTestDefinition
            { Name = "switcher", Kind = "smoke", Scene = isScene ? options.Switcher : "res://scenes/menu/launcher.tscn", Args = args.ToArray(),
                TimeoutSeconds = options.TimeoutSeconds + 60, Result = new ResultContract { Name = "test_runner" } } } };
        }
    }
}
