namespace Cthangover.TestRunner
{
    using System.Text;
    using System.Text.Json;
    using System.Diagnostics;

    /// <summary>Plans first, builds once, executes dependency groups in a private workspace and persists every outcome.</summary>
    public static class Program
    {
        /// <summary>Plans and executes one invocation; success requires verified nonempty terminal results and complete cleanup.</summary>
        public static int Main(string[] args)
        {
            using var lifetime = new RunnerLifetime();
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch
            {
            }
            try
            {
                return Run(RunnerOptions.Parse(args));
            }
            catch (Exception error)
            {
                RunnerConsole.WriteError("test runner error: " + error.Message);
                return 2;
            }
        }

        private static int Run(RunnerOptions options)
        {
            var startedAt = DateTimeOffset.UtcNow;
            if (options.ShowHelp)
            {
                RunnerConsole.WriteLine(RunnerOptions.HelpText);
                return 0;
            }

            var root = ProjectLocator.Locate();
            var manifest = TestManifest.LoadProject(root);
            var catalog = TestCatalog.Load(root, manifest);
            if (options.List || options.Catalog != null)
            {
                var listed = SelectCatalog(catalog, options);
                if (listed.Count == 0)
                {
                    throw new ArgumentException("catalogue selection is empty");
                }

                foreach (var test in listed)
                {
                    RunnerConsole.WriteLine($"{test.Name,-38} {test.Kind,-12} owners={string.Join(',', test.Owners)} "
                        + $"profiles={string.Join(',', test.Profiles)} {(test.Windowed ? "windowed" : "headless")} {test.ManualReason}");
                }

                if (options.Catalog != null)
                {
                    TestCatalog.Write(options.Catalog, listed);
                }

                return 0;
            }

            var plan = options.Switcher == null ? TestPlanner.Build(catalog, options) : SmokePlan(options);
            if (options.Plan)
            {
                foreach (var entry in plan)
                {
                    var mode = entry.Test.Project == "write" ? "persistent-write" : "prepared-" + entry.Test.Project;
                    RunnerConsole.WriteLine($"{entry.Test.Name,-38} {entry.Reason}; env={entry.Environment}; "
                        + $"workspace={mode}; depends={string.Join(',', entry.Test.DependsOn)}; filter={entry.Filter}");
                }

                return 0;
            }

            using var extraOutput = new ReportOutputClaim(options.Json);
            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var artifacts = Path.Combine(Path.GetFullPath(options.Artifacts ?? Path.Combine(root, "tests", "artifacts", "runs")), runId);
            Directory.CreateDirectory(artifacts);
            var report = new RunReport
            {
                RunId = runId,
                StartedAt = startedAt
            };
            ReportWriter.Write(artifacts, report, options.Json);
            RunnerConsole.WriteLine("Запуск " + runId + "; отчёты: " + artifacts);
            PreparedInputLease prepared = null;
            ParallelExecution parallel = null;
            try
            {
                WorkspaceRetention.Prune(Path.GetTempPath(), options.RetentionCount, options.RetentionDays, options.RetentionBytes);
                CompilationCachePolicy.Prune(CompilationCachePolicy.Resolve(Path.GetTempPath(), runId, false), DateTime.UtcNow);
                var build = new PreparedBuildRequest
                {
                    IncludeUnit = plan.Any(IsUnit),
                    NoBuild = options.NoBuild,
                    ArtifactsDirectory = artifacts
                };
                prepared = options.RepairInputs
                    ? PreparedInputStore.Repair(root, options.InputCache, build, RunnerLifetime.Token)
                    : PreparedInputStore.Acquire(root, options.InputCache, build, RunnerLifetime.Token);
                report.InputPreparation = prepared.Metrics;
                prepared.SetRunId(runId);
                report.Phases.BuildSeconds = prepared.Metrics.BuildSeconds;
                report.Phases.WorkspaceCopySeconds = prepared.Metrics.PrepareSeconds;
                // Plan/cataloque are rebuilt from the sealed inputs, never combined with earlier live manifest bytes.
                manifest = TestManifest.LoadProject(prepared.Root);
                catalog = TestCatalog.Load(prepared.Root, manifest);
                plan = options.Switcher == null ? TestPlanner.Build(catalog, options) : SmokePlan(options);
                report.Results = plan.Select(Pending).ToList();
                PreparedInputJson.Write(Path.Combine(artifacts, "plan.json"), plan);
                TestCatalog.Write(Path.Combine(artifacts, "catalog.json"), catalog);
                parallel = new ParallelExecution(prepared, runId, artifacts, manifest.Unit, plan, options, report);
                parallel.Run();
                return Finish(artifacts, report, options, null, parallel, prepared: prepared);
            }
            catch (Exception error)
            {
                RunnerLifetime.Cancel();
                if (error is PreparedInputRefusal refusal)
                {
                    report.InputPreparation = refusal.Metrics;
                }

                report.Results.Add(new TestResult
                {
                    Name = "infrastructure",
                    Kind = "infrastructure",
                    ExitCode = -1,
                    FailureKind = "infrastructure",
                    Note = error.Message,
                    Diagnostics = new() { error.ToString() }
                });
                return Finish(artifacts, report, options, null, parallel, complete: false, prepared: prepared);
            }
            finally
            {
                prepared?.Dispose();
            }
        }

        private static bool IsUnit(TestPlanEntry entry)
        {
            return entry.Test.Kind == "unit";
        }

        internal static TestResult Pending(TestPlanEntry entry) => new()
        {
            Name = entry.Test.Name,
            Kind = entry.Test.Kind,
            Blocked = true,
            ExitCode = -1,
            Note = "not started",
            Environment = entry.Environment,
            Filter = entry.Filter,
            SelectionReason = entry.Reason,
            DependsOn = entry.Test.DependsOn,
            ModFilter = entry.ModFilter,
            FailureKind = "not-started",
        };

        private static int Finish(string directory, RunReport report, RunnerOptions options, TestWorkspace workspace,
            ParallelExecution parallel = null, bool complete = true, PreparedInputLease prepared = null)
        {
            parallel?.Dispose();
            if (workspace != null)
            {
                report.Phases.RestoreSeconds = workspace.RestoreSeconds;
                report.Phases.RestoreCount = workspace.RestoreCount;
                var cleanupStarted = Stopwatch.GetTimestamp();
                try
                {
                    workspace.Dispose();
                }
                catch (Exception error)
                {
                    workspace.Keep = true;
                    report.Results.Add(new TestResult
                    {
                        Name = "cleanup",
                        Kind = "infrastructure",
                        ExitCode = -1,
                        FailureKind = "cleanup",
                        Note = "workspace cleanup failed: " + error.Message,
                        Diagnostics = new() { error.ToString() }
                    });
                }
                finally
                {
                    report.Phases.CleanupSeconds = Stopwatch.GetElapsedTime(cleanupStarted).TotalSeconds;
                }
                report.WorkspaceRetained = workspace.Keep;
                if (workspace.Keep)
                    report.RetainedWorkspaces.Add(workspace.RunRoot);
            }
            prepared?.Dispose();
            report.RetentionDecisions.AddRange(PreparedInputStore.Prune(options.InputCache, options.RetentionCount,
                options.RetentionDays, options.RetentionBytes));
            report.Complete = complete;
            report.FinishedAt = DateTimeOffset.UtcNow;
            ReportWriter.Write(directory, report, options.Json);
            WorkspaceRetention.Prune(Path.GetTempPath(), options.RetentionCount, options.RetentionDays, options.RetentionBytes);
            report.RetainedWorkspaces.RemoveAll(MissingDirectory);
            report.WorkspaceRetained = report.RetainedWorkspaces.Count > 0;
            ReportWriter.Write(directory, report, options.Json);
            SummaryPrinter.Print(report.Build == null ? report.Results : report.Results.Prepend(report.Build).ToList());
            SummaryPrinter.PrintTiming(report);
            RunnerConsole.WriteLine("JSON / HTML / JUnit: " + directory);
            if (report.WorkspaceRetained)
                RunnerConsole.WriteLine("Окружение сохранено: " + string.Join(", ", report.RetainedWorkspaces));
            return report.Success ? 0 : 1;
        }

        internal static TestResult RunUnit(TestWorkspace workspace, UnitManifest unit, TestPlanEntry entry, string directory,
            string launchName = null, CancellationToken cancellationToken = default, string runId = null, string workerId = "main")
        {
            using var context = new LaunchExecutionContext(workspace, runId ?? "standalone", entry.Test.Name, workerId, directory);
            directory = context.Metrics.OutputDirectory;
            var assembly = Path.Combine(workspace.Root, Path.GetDirectoryName(unit.Project), "bin", "Debug", "net10.0",
                Path.GetFileNameWithoutExtension(unit.Project) + ".dll");
            if (!File.Exists(assembly))
            {
                throw new FileNotFoundException("built unit-test assembly not found", assembly);
            }

            var args = new List<string>
            {
                "vstest",
                assembly,
                "--logger:trx;LogFileName=unit.trx",
                "--logger:console;verbosity=normal",
                "--ResultsDirectory:" + directory
            };
            var settings = launchName == null ? null : UnitRunSettings.Write(directory);
            if (settings != null)
            {
                args.Add("--Settings:" + settings);
            }

            if (entry.Filter != null)
            {
                args.Add("--TestCaseFilter:" + entry.Filter);
            }

            var working = Path.Combine(context.Metrics.TemporaryDirectory, "unit-work");
            Directory.CreateDirectory(working);
            var run = ProcessRunner.Run("dotnet", args, working, entry.Test.TimeoutSeconds,
                Path.Combine(directory, "process.log"), context.EnvironmentVariables(), launchName, cancellationToken, context);
            var trx = TrxParser.Read(Path.Combine(directory, "unit.trx"));
            var result = new TestResult
            {
                Name = entry.Test.Name,
                Kind = "unit",
                Execution = context.Metrics,
                Passed = run.ExitCode == 0 && !run.TimedOut && trx.Failed == 0 && trx.Problems.Count == 0,
                ExitCode = run.ExitCode,
                TimedOut = run.TimedOut,
                Duration = run.Duration,
                LogFile = run.LogFile,
                BootSeconds = run.BootSeconds,
                CpuSeconds = run.CpuSeconds,
                PeakMemoryMb = run.PeakMemoryMb,
                LogLines = run.LogLines,
                PassedChecks = trx.Passed,
                FailedChecks = trx.Failed,
                SkippedChecks = trx.Skipped,
                Cases = trx.Cases,
                Note = string.Join("; ", trx.Problems),
                FailureKind = run.TimedOut ? "timeout" : trx.Problems.Count > 0 ? "contract"
                    : trx.Failed > 0 ? "assertion" : run.ExitCode == 0 ? null : "process"
            };
            if (!result.Passed)
            {
                result.Diagnostics = TestLogs.Highlights(run.Output);
            }

            if (context.InputViolation)
            {
                result.Passed = false;
                result.FailureKind = "input-integrity";
                result.Note += "; immutable input mutation";
            }

            return result;
        }

        internal static TestResult RunIntegration(TestWorkspace workspace, string godot, TestPlanEntry entry, string directory,
            string launchName = null, CancellationToken cancellationToken = default, string runId = null, string workerId = "main")
        {
            var test = entry.Test;
            var problem = ScenePathValidator.Check(workspace.Root, test.Scene);
            if (problem != null)
            {
                throw new InvalidOperationException(problem);
            }

            using var context = new LaunchExecutionContext(workspace, runId ?? "standalone", test.Name, workerId, directory);
            if (test.Project == "output" && context.Metrics.WorkspaceMode == "prepared-read")
            {
                context.Metrics.WorkspaceMode = "prepared-output";
            }

            var args = context.EngineArguments(test.Scene, !test.Windowed);
            var userArgs = new List<string>(test.Args ?? Array.Empty<string>());
            if (!userArgs.Any(IsGameChoice))
            {
                userArgs.Add("--all-mods");
            }

            if (!userArgs.Contains("--hot-reload"))
            {
                userArgs.Add("--no-hot-reload");
            }

            if (entry.ModFilter != null)
            {
                userArgs.Add("--mod=" + entry.ModFilter);
            }

            if (entry.Filter != null)
            {
                userArgs.Add("--test-filter=" + entry.Filter);
            }

            args.AddRange(context.UserArguments(userArgs));
            var modReport = Path.Combine(context.Metrics.OutputDirectory, "mod-tests", "last-report.json");
            var run = ProcessRunner.Run(godot, args, workspace.Root, test.TimeoutSeconds,
                Path.Combine(context.Metrics.OutputDirectory, "process.log"), context.EnvironmentVariables(),
                launchName, cancellationToken, context);
            var result = ResultParser.Evaluate(test.Name, test.Kind, run, test.Result);
            result.Execution = context.Metrics;
            if (run.Output.Any(IsDataHomeMismatch) || !VerifiedContext(context.Metrics, run.ProcessId))
            {
                result.Passed = false;
                result.FailureKind = "infrastructure";
                result.Note += "; early execution context was not verified";
            }

            if (context.InputViolation)
            {
                result.Passed = false;
                result.FailureKind = "input-integrity";
                result.Note += "; immutable input mutation";
            }

            if (test.Kind == "mod-unit")
            {
                try
                {
                    ModReportReader.Import(modReport, result);
                }
                catch (Exception error)
                {
                    result.Passed = false;
                    result.FailureKind ??= "contract";
                    result.Note += "; " + error.Message;
                }
            }

            return result;
        }

        private static bool IsGameChoice(string argument)
        {
            return argument.StartsWith("--game=", StringComparison.Ordinal) || argument == "--all-mods" || argument == "--frontend";
        }

        private static bool VerifiedContext(LaunchExecutionMetrics metrics, int? processId)
        {
            var path = Path.Combine(metrics.OutputDirectory, "core-context.json");
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                var root = json.RootElement;
                return root.GetProperty("phase").GetString() == "verified"
                    && root.GetProperty("runId").GetString() == metrics.RunId
                    && root.GetProperty("testId").GetString() == metrics.TestId
                    && root.GetProperty("launchId").GetString() == metrics.LaunchId
                    && root.GetProperty("osPid").GetInt32() == processId;
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                return false;
            }
        }

        private static bool IsDataHomeMismatch(string line)
        {
            return line.Contains("Test user data isolation mismatch", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MissingDirectory(string path)
        {
            return !Directory.Exists(path);
        }

        private static List<IntegrationTestDefinition> SelectCatalog(
            IReadOnlyList<IntegrationTestDefinition> catalog, RunnerOptions options)
        {
            var listed = new List<IntegrationTestDefinition>();
            foreach (var test in catalog)
            {
                if ((options.Mod != null && !test.Owners.Contains(options.Mod))
                    || (options.Profile != null && !test.Profiles.Contains(options.Profile))
                    || (options.Kind != null && test.Kind != options.Kind)
                    || (options.Tag != null && !test.Tags.Contains(options.Tag))
                    || (options.Game != null && test.Game != options.Game)
                    || (options.Renderer != null && test.Renderer != options.Renderer))
                {
                    continue;
                }
                var matches = options.Tests.Count == 0;
                foreach (var pattern in options.Tests)
                {
                    matches |= NamePattern.Matches(test.Name, pattern);
                }
                if (matches)
                {
                    listed.Add(test);
                }
            }
            return listed;
        }

        private static List<TestPlanEntry> SmokePlan(RunnerOptions options)
        {
            var args = new List<string>
            {
                "--",
                "--all-mods",
                "--timeout=" + options.TimeoutSeconds
            };
            var isScene = options.Switcher.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase);
            if (!isScene)
                args.Add("--scene=" + options.Switcher);
            if (options.Choices != null)
                args.Add("--choices=" + options.Choices);
            return new()
            {
                new TestPlanEntry
                {
                    Environment = "switcher",
                    Reason = "smoke scene",
                    Test = new IntegrationTestDefinition
                    {
                        Name = "switcher",
                        Kind = "smoke",
                        Scene = isScene ? options.Switcher : "res://scenes/menu/launcher.tscn",
                        Args = args.ToArray(),
                        TimeoutSeconds = options.TimeoutSeconds + 60,
                        Result = new ResultContract { Name = "test_runner" }
                    }
                }
            };
        }
    }
}
