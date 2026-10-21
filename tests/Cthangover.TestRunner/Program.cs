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
                    FailedChecks = counters?.Failed ?? 0,
                    LogFile = TestLogs.Save(root, "unit", run),
                };
                if (counters?.Failed > 0)
                    result.Passed = false;
                if (counters == null)
                    result.Note = "trx counters unavailable";
                else if (counters.Value.Skipped > 0)
                    result.Note = $"skipped={counters.Value.Skipped}";
                if (!result.Passed)
                    result.Diagnostics = run.Output
                        .Where(l => l.Contains("[FAIL]", StringComparison.Ordinal) || l.TrimStart().StartsWith("Failed ", StringComparison.Ordinal))
                        .Select(l => l.Trim())
                        .Take(12)
                        .ToList();
                return result;
            }
            finally
            {
                try { Directory.Delete(trxDirectory, true); } catch { }
            }
        }

        /// <summary>
        /// A plain launch boots only the front-end (main menu + mod manager), so
        /// every integration test that does not name a game gets
        /// <c>--all-mods</c> after the <c>--</c> separator, keeping the scenes
        /// on the full mod set they were written for.
        /// </summary>
        private static List<string> WithModSet(IReadOnlyList<string> testArgs)
        {
            var args = new List<string>(testArgs ?? Array.Empty<string>());
            if (args.Any(a => a.StartsWith("--game=", StringComparison.Ordinal) || a == "--all-mods" || a == "--frontend"))
                return args;

            if (!args.Contains("--"))
                args.Add("--");
            args.Add("--all-mods");
            return args;
        }

        /// <summary>
        /// Integration runs are automated: the mod hot reload watcher (on for
        /// any run from the editor binary) only costs a recursive watch of
        /// every mod root and could reload under a test that writes mod files,
        /// so it is switched off unless the test opts in with
        /// <c>--hot-reload</c> (HotReloadSettings.EnableFlag).
        /// </summary>
        private static List<string> WithoutHotReload(List<string> args)
        {
            if (!args.Contains("--hot-reload"))
                args.Add("--no-hot-reload");
            return args;
        }

        /// <summary>
        /// Runs one integration scene headless and evaluates it from three
        /// sources: process exit code, the scene's <c>[TEST_RESULT]</c> line
        /// (authoritative for check counts) and the timeout flag.
        /// </summary>
        private static TestResult RunIntegration(string root, string godot, IntegrationTestDefinition test)
        {
            // A wrong path (typically wrong case: fine on Windows, broken on
            // Linux/CI and in exported packs) fails here with a precise
            // message instead of a Godot run that ends in "no result marker".
            var sceneProblem = ScenePathValidator.Check(root, test.Scene);
            if (sceneProblem != null)
            {
                return new TestResult
                {
                    Name = test.Name,
                    Kind = "integration",
                    Passed = false,
                    ExitCode = -1,
                    FailedChecks = 1,
                    Note = sceneProblem,
                };
            }

            var args = new List<string>();
            if (!test.Windowed)
                args.Add("--headless");
            args.Add("--path");
            args.Add(root);
            args.Add(test.Scene);
            args.AddRange(WithoutHotReload(WithModSet(test.Args)));

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
                Note = run.TimedOut ? "timeout" : (!parsed.HasResult ? "no result marker" : null),
                LogFile = TestLogs.Save(root, test.Name, run),
                Diagnostics = passed ? new List<string>() : TestLogs.Highlights(run.Output),
            };
        }

        /// <summary>
        /// Smoke run of an arbitrary scene through the native
        /// <c>--scene=</c> launch (kernel navigation) plus the
        /// <c>test_runner</c> engine mod, which auto-plays the dialog and
        /// prints the authoritative <c>[TEST_RESULT]</c>. A <c>.tscn</c>
        /// target is passed positionally (Godot loads it natively); a
        /// logical scene name goes through user args after <c>--</c>. The
        /// driver's flags are always user args — the mod reads
        /// <c>OS.GetCmdlineUserArgs()</c>, so they never spuriously activate
        /// it during regular integration entries.
        /// </summary>
        private static TestResult RunSwitcher(string root, string godot, RunnerOptions options)
        {
            var isTscn = options.Switcher.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase);
            var args = new List<string> { "--headless", "--path", root };
            if (isTscn)
                args.Add(options.Switcher);
            args.Add("--");
            if (!isTscn)
                args.Add($"--scene={options.Switcher}");
            args.Add($"--timeout={options.TimeoutSeconds}");
            if (!string.IsNullOrEmpty(options.Choices))
                args.Add($"--choices={options.Choices}");

            Console.WriteLine($"SceneSwitcher: {options.Switcher}...");
            var run = ProcessRunner.Run(godot, args, root, options.TimeoutSeconds + 60);
            var parsed = ResultParser.Parse(run.Output);
            var passed = !run.TimedOut && run.ExitCode == 0 && parsed.HasResult && parsed.Failed == 0;

            return new TestResult
            {
                Name = "switcher",
                Kind = "smoke",
                Passed = passed,
                TimedOut = run.TimedOut,
                ExitCode = run.ExitCode,
                PassedChecks = parsed.HasResult ? parsed.Passed : (passed ? 1 : 0),
                FailedChecks = parsed.HasResult ? parsed.Failed : (passed ? 0 : 1),
                Duration = run.Duration,
                LogFile = TestLogs.Save(root, "switcher", run),
                Diagnostics = passed ? new List<string>() : TestLogs.Highlights(run.Output),
                Note = passed
                    ? $"scene={options.Switcher}"
                    : parsed.HasResult && parsed.Failed > 0
                        ? $"driver timeout/fail: scene={options.Switcher}"
                        : $"no result: scene={options.Switcher}"
            };
        }
    }
}
