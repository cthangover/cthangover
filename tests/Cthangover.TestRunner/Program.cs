using System.Text;

namespace Cthangover.TestRunner
{
    /// <summary>
    /// Cross-platform entry point for the whole test suite: builds the game
    /// assembly, runs xUnit unit tests, then every integration scene from
    /// <c>tests/tests.manifest.json</c> sequentially (they share the mod cache
    /// and write into <c>mods/</c>, so parallel runs are unsafe), and prints
    /// one summary with an aggregate exit code. Replaces the Windows-only
    /// PowerShell scripts; requires only the .NET 8 SDK and a Godot .NET
    /// executable on PATH, via <c>GODOT_BIN</c> or <c>--godot</c>.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"test runner error: {ex.Message}");
                return 2;
            }
        }

        private static int Run(string[] args)
        {
            var options = RunnerOptions.Parse(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(RunnerOptions.HelpText);
                return 0;
            }

            var root = ProjectLocator.Locate();
            var manifest = TestManifest.Load(Path.Combine(root, "tests", "tests.manifest.json"));

            if (options.List)
            {
                Console.WriteLine($"{"unit",-14} {manifest.Unit.Project}");
                foreach (var test in manifest.Integration)
                    Console.WriteLine($"{test.Name,-14} {test.Scene}");
                return 0;
            }

            var godot = GodotLocator.Locate(options.Godot);
            var results = new List<TestResult>();

            if (!string.IsNullOrEmpty(options.Switcher))
            {
                if (!EnsureBuild(root, options.NoBuild))
                    return 1;
                results.Add(RunSwitcher(root, godot, options));
                return SummaryPrinter.Print(results, options.Json);
            }

            var selected = manifest.Integration.FirstOrDefault(t => t.Name == options.Test);
            var runUnit = string.IsNullOrEmpty(options.Test) || options.Test == "unit";
            var runAllIntegration = string.IsNullOrEmpty(options.Test);

            if (!runUnit && selected == null)
                throw new InvalidOperationException($"unknown test '{options.Test}'; use --list to see available tests");

            if ((runAllIntegration || selected != null) && !EnsureBuild(root, options.NoBuild))
                return 1;

            if (runUnit)
            {
                Console.WriteLine("Unit-тесты...");
                results.Add(RunUnit(root, manifest));
            }

            if (runAllIntegration)
            {
                foreach (var test in manifest.Integration)
                {
                    Console.WriteLine($"Интеграционный тест: {test.Name}...");
                    results.Add(RunIntegration(root, godot, test));
                }
            }
            else if (selected != null)
            {
                Console.WriteLine($"Интеграционный тест: {selected.Name}...");
                results.Add(RunIntegration(root, godot, selected));
            }

            return SummaryPrinter.Print(results, options.Json);
        }

        /// <summary>
        /// Builds the game assembly so Godot loads the current code (integration
        /// scenes and SceneSwitcher both run against it). Returns <c>false</c>
        /// after printing the compiler output when the build fails, so the
        /// caller can exit before reporting misleading test results.
        /// </summary>
        private static bool EnsureBuild(string root, bool noBuild)
        {
            if (noBuild)
                return true;

            Console.WriteLine("Сборка Cthangover.csproj...");
            var build = ProcessRunner.Run("dotnet", new[] { "build", "Cthangover.csproj", "--nologo", "-v", "q" }, root, 300);
            if (!build.TimedOut && build.ExitCode == 0)
                return true;

            Console.Error.WriteLine("Сборка не удалась:");
            Console.Error.WriteLine(build.OutputText);
            return false;
        }

        /// <summary>
        /// Runs the xUnit project through <c>dotnet test</c> with a TRX logger
        /// and reads exact counters from the report. TRX avoids parsing the
        /// localized console summary.
        /// </summary>
        private static TestResult RunUnit(string root, TestManifest manifest)
        {
            var trxDirectory = Path.Combine(Path.GetTempPath(), "cthangover_trx_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(trxDirectory);
            try
            {
                var args = new List<string>
                {
                    "test", manifest.Unit.Project, "--nologo", "-v", "q",
                    "--logger", "trx;LogFileName=unit.trx",
                    "--results-directory", trxDirectory
                };
                var run = ProcessRunner.Run("dotnet", args, root, manifest.Unit.TimeoutSeconds);
                var counters = TrxParser.TryParseCounters(Path.Combine(trxDirectory, "unit.trx"));

                var result = new TestResult
                {
                    Name = "unit",
                    Kind = "unit",
                    ExitCode = run.ExitCode,
                    Duration = run.Duration,
                    TimedOut = run.TimedOut,
                    Passed = !run.TimedOut && run.ExitCode == 0,
                    PassedChecks = counters?.Passed ?? 0,
                    FailedChecks = counters?.Failed ?? 0
                };
                if (counters?.Failed > 0)
                    result.Passed = false;
                if (counters == null)
                    result.Note = "trx counters unavailable";
                else if (counters.Value.Skipped > 0)
                    result.Note = $"skipped={counters.Value.Skipped}";
                return result;
            }
            finally
            {
                try { Directory.Delete(trxDirectory, true); } catch { }
            }
        }

        /// <summary>
        /// Runs one integration scene headless and evaluates it from three
        /// sources: process exit code, the scene's <c>[TEST_RESULT]</c> line
        /// (authoritative for check counts) and the timeout flag.
        /// </summary>
        private static TestResult RunIntegration(string root, string godot, IntegrationTestDefinition test)
        {
            var args = new List<string> { "--headless", "--path", root, test.Scene };
            if (test.Args != null)
                args.AddRange(test.Args);

            var run = ProcessRunner.Run(godot, args, root, test.TimeoutSeconds);
            var parsed = ResultParser.Parse(run.Output);
            var passed = !run.TimedOut && run.ExitCode == 0 && (!parsed.HasResult || parsed.Failed == 0);

            return new TestResult
            {
                Name = test.Name,
                Kind = "integration",
                Passed = passed,
                TimedOut = run.TimedOut,
                ExitCode = run.ExitCode,
                PassedChecks = parsed.HasResult ? parsed.Passed : (passed ? 1 : 0),
                FailedChecks = parsed.HasResult ? parsed.Failed : (passed ? 0 : 1),
                Duration = run.Duration,
                Core = parsed.Core,
                Coverage = parsed.Coverage,
                Note = run.TimedOut ? "timeout" : (!parsed.HasResult ? "no result marker" : null)
            };
        }

        /// <summary>
        /// Smoke run through <c>SceneSwitcher</c> for arbitrary scenes and
        /// logical scene names. Unlike self-checking suites this has no
        /// <c>[TEST_RESULT]</c>; success means the auto-driver finished the
        /// dialog without hitting the SceneSwitcher timeout.
        /// </summary>
        private static TestResult RunSwitcher(string root, string godot, RunnerOptions options)
        {
            var args = new List<string>
            {
                "--headless", "--path", root,
                "res://scenes/test/scene_switcher.tscn",
                $"--scene={options.Switcher}",
                $"--timeout={options.TimeoutSeconds}"
            };
            if (!string.IsNullOrEmpty(options.Choices))
                args.Add($"--choices={options.Choices}");

            Console.WriteLine($"SceneSwitcher: {options.Switcher}...");
            var run = ProcessRunner.Run(godot, args, root, options.TimeoutSeconds + 60);
            var ended = run.Output.Any(line => line.Contains("DialogAutoDriver: dialog ended, quitting"));
            var timedOut = run.Output.Any(line => line.Contains("SceneSwitcher: timeout"));
            var passed = !run.TimedOut && run.ExitCode == 0 && ended && !timedOut;

            return new TestResult
            {
                Name = "switcher",
                Kind = "smoke",
                Passed = passed,
                TimedOut = run.TimedOut,
                ExitCode = run.ExitCode,
                PassedChecks = passed ? 1 : 0,
                FailedChecks = passed ? 0 : 1,
                Duration = run.Duration,
                Note = passed
                    ? $"scene={options.Switcher}"
                    : timedOut
                        ? $"timeout: scene={options.Switcher}"
                        : $"no dialog end: scene={options.Switcher}"
            };
        }
    }
}
