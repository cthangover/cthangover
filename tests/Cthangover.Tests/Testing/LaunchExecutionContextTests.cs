namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.TestRunner;

    /// <summary>Checks parent argv/env against the real Core parser and keeps process artifacts independent of component saves.</summary>
    [Collection("runner-infrastructure")]
    public sealed class LaunchExecutionContextTests : IDisposable
    {
        private readonly PreparedInputFixture fixture = new();

        /// <summary>Concurrent read components borrow one prepared tree but expose different DATA, temp and every output sink.</summary>
        [Fact]
        public void ConcurrentComponentsUseSeparateHomesAndLaunchRootsWithNoInputCopies()
        {
            using var inputs = fixture.Acquire();
            using var workspace = Workspace(inputs);
            workspace.ConfigureShared();
            using var first = workspace.SharedComponent("first");
            using var second = workspace.SharedComponent("second");
            using var launchA = Launch(first, "first");
            using var launchB = Launch(second, "second");
            var a = Parse(launchA);
            var b = Parse(launchB);
            Assert.Equal(inputs.Root, a.ProjectRoot);
            Assert.Equal(a.ProjectRoot, b.ProjectRoot);
            Assert.Equal(a.PreparedRevision, b.PreparedRevision);
            Assert.Equal(a.InputIdentity, b.InputIdentity);
            Assert.NotEqual(a.LaunchId, b.LaunchId);
            Assert.NotEqual(a.UserDataRoot, b.UserDataRoot);
            Assert.NotEqual(a.TemporaryRoot, b.TemporaryRoot);
            Assert.NotEqual(a.ArtifactRoot, b.ArtifactRoot);
            Assert.NotEqual(a.MainLogPath, b.MainLogPath);
            Assert.NotEqual(a.ErrorLogPath, b.ErrorLogPath);
            Assert.NotEqual(a.NativeLogPath, b.NativeLogPath);
            Assert.NotEqual(a.ReportDirectory, b.ReportDirectory);
            Assert.NotEqual(a.ScreenshotsDirectory, b.ScreenshotsDirectory);
            Assert.NotEqual(a.SandboxDirectory, b.SandboxDirectory);
            Assert.Equal(0, first.Metrics.FullProjectCopies);
            Assert.Equal(0, second.Metrics.FullProjectCopies);
            Assert.Equal(0, launchA.Metrics.InputsCopied.Files);
            Assert.Equal(0, launchA.Metrics.InputsCopied.Bytes);
            Assert.Equal(0, launchB.Metrics.InputsCopied.Files);
            Assert.Equal(0, launchB.Metrics.InputsCopied.Bytes);
            WriteToken(a);
            WriteToken(b);
            Assert.Equal(a.LaunchId, File.ReadAllText(a.MainLogPath));
            Assert.Equal(b.LaunchId, File.ReadAllText(b.MainLogPath));
            Assert.Empty(inputs.VerifyUnchanged());
        }

        /// <summary>Sequential launches retain their component save while process outputs and temp remain unique.</summary>
        [Fact]
        public void SequentialLaunchesShareOnlyTheirDeclaredComponentState()
        {
            using var inputs = fixture.Acquire();
            using var workspace = Workspace(inputs);
            workspace.ConfigureShared();
            using var component = workspace.SharedComponent("chain");
            using var first = Launch(component, "setup");
            var a = Parse(first);
            Directory.CreateDirectory(a.UserDataRoot);
            File.WriteAllText(Path.Combine(a.UserDataRoot, "save.json"), "setup-state");
            using var second = Launch(component, "dependent");
            var b = Parse(second);
            Assert.Equal(a.UserDataRoot, b.UserDataRoot);
            Assert.Equal("setup-state", File.ReadAllText(Path.Combine(b.UserDataRoot, "save.json")));
            Assert.NotEqual(a.LaunchId, b.LaunchId);
            Assert.NotEqual(a.ArtifactRoot, b.ArtifactRoot);
            Assert.NotEqual(a.TemporaryRoot, b.TemporaryRoot);
            Assert.NotEqual(a.ReportDirectory, b.ReportDirectory);
        }

        /// <summary>Pre-start receipt and actual native argv agree with Core even when the engine consumes its log flag.</summary>
        [Fact]
        public void NativeAndUserArgumentsValidateAgainstCoreAndPrestartReceipt()
        {
            using var inputs = fixture.Acquire();
            using var workspace = Workspace(inputs);
            workspace.Begin("probe");
            using var launch = Launch(workspace, "probe");
            var context = Parse(launch);
            var engine = launch.EngineArguments("res://scenes/probe.tscn", true);
            Assert.Contains("--headless", engine);
            Assert.Contains("--log-file", engine);
            Assert.Contains(context.NativeLogPath, engine);
            Assert.DoesNotContain("--test-output-dir=" + context.ArtifactRoot, engine);
            Assert.Equal("engine-argument", context.ValidateRuntimePaths(context.ProjectRoot,
                context.UserDataRoot, context.TemporaryRoot, engine));
            var path = Path.Combine(context.ArtifactRoot, "launch.json");
            Assert.True(File.Exists(path));
            var receipt = File.ReadAllText(path);
            Assert.Equal("parent-launch-receipt", context.ValidateRuntimePaths(context.ProjectRoot,
                context.UserDataRoot, context.TemporaryRoot, Array.Empty<string>(), receipt));
            var restored = JsonSerializer.Deserialize<LaunchExecutionReceipt>(receipt, ReportWriter.JsonOptions);
            Assert.NotNull(restored);
            Assert.Equal("starting", restored.Status);
            Assert.Null(restored.ExitCode);
            Assert.Equal(context.RunId, restored.RunId);
            Assert.Equal(context.TestId, restored.TestId);
            Assert.Equal(context.LaunchId, restored.LaunchId);
            Assert.Equal(context.NativeLogPath, restored.NativeLogPath);
            Assert.Equal(engine, restored.EngineArguments);
            var canonical = JsonSerializer.Serialize(restored, ReportWriter.JsonOptions);
            var roundTrip = JsonSerializer.Deserialize<LaunchExecutionReceipt>(canonical, ReportWriter.JsonOptions);
            Assert.Equal(canonical, JsonSerializer.Serialize(roundTrip, ReportWriter.JsonOptions));
        }

        /// <summary>Observed terminal metadata preserves a timeout/nonzero exit and cannot turn readiness into a successful test.</summary>
        [Fact]
        public void TerminalReceiptKeepsObservedFailureAndMetricsThroughJsonRoundTrip()
        {
            using var inputs = fixture.Acquire();
            using var workspace = Workspace(inputs);
            workspace.Begin("probe");
            using var launch = Launch(workspace, "probe");
            var context = Parse(launch);
            launch.EngineArguments("res://scenes/probe.tscn", true);
            // A timeout the supervisor observed end-to-end: root exit confirmed
            // and both pipes drained, so CleanupSafe holds and the receipt keeps
            // the observed "timed_out" status instead of the supervised
            // "unproven-exit"/"unfinished-pipes" fallbacks.
            launch.WriteTerminal(new ProcessResult
            {
                ExitCode = 124,
                ObservedExitCode = 124,
                ProcessId = 12345,
                ExitConfirmed = true,
                StdoutCompleted = true,
                StderrCompleted = true,
                TimedOut = true,
                Duration = TimeSpan.FromSeconds(2.5),
                CpuSeconds = 0.25,
                PeakMemoryMb = 32
            });
            var receipt = JsonSerializer.Deserialize<LaunchExecutionReceipt>(
                File.ReadAllText(Path.Combine(context.ArtifactRoot, "launch.json")), ReportWriter.JsonOptions);
            Assert.NotNull(receipt);
            Assert.Equal("timed_out", receipt.Status);
            Assert.True(receipt.ExitConfirmed);
            Assert.Equal(124, receipt.ObservedExitCode);
            Assert.Equal(124, receipt.ExitCode);
            Assert.Equal(12345, receipt.ProcessId);
            Assert.Equal(2.5, receipt.ElapsedSeconds);
            Assert.Equal(0.25, receipt.CpuSeconds);
            Assert.Equal(32, receipt.PeakMemoryMb);
            Assert.Equal(context.LaunchId, receipt.Execution.LaunchId);
            Assert.Equal(context.ArtifactRoot, receipt.Execution.OutputDirectory);
            Assert.Equal(0, receipt.Execution.InputsCopied.Files);
            Assert.Equal(0, receipt.Execution.InputsCopied.Bytes);
            var serialized = JsonSerializer.Serialize(receipt, ReportWriter.JsonOptions);
            var restored = JsonSerializer.Deserialize<LaunchExecutionReceipt>(serialized, ReportWriter.JsonOptions);
            Assert.Equal(serialized, JsonSerializer.Serialize(restored, ReportWriter.JsonOptions));
        }

        /// <summary>Child env dictionaries cannot mutate the parent process and snapshot changes cannot alter another launch.</summary>
        [Fact]
        public void EnvironmentAndArtifactsAreOwnedByLaunchInsteadOfGlobalProcess()
        {
            var previous = Environment.GetEnvironmentVariable("CTHANGOVER_TEST_LAUNCH_ID");
            using var inputs = fixture.Acquire();
            using var workspace = Workspace(inputs);
            workspace.Begin("probe");
            using var launch = Launch(workspace, "probe");
            var environment = launch.EnvironmentVariables();
            var context = TestExecutionContext.Parse(environment, Array.Empty<string>());
            Assert.NotNull(context);
            Assert.Equal(previous, Environment.GetEnvironmentVariable("CTHANGOVER_TEST_LAUNCH_ID"));
            environment["CTHANGOVER_TEST_LAUNCH_ID"] = "peer";
            Assert.Equal(context.LaunchId, launch.EnvironmentVariables()["CTHANGOVER_TEST_LAUNCH_ID"]);
            Assert.NotEqual(context.MainLogPath, context.ErrorLogPath);
            Assert.NotEqual(context.MainLogPath, context.NativeLogPath);
            Assert.NotEqual(context.ErrorLogPath, context.NativeLogPath);
        }

        private TestWorkspace Workspace(PreparedInputLease inputs)
        {
            return new TestWorkspace(inputs, Guid.NewGuid().ToString("N"), artifactsDirectory: Path.Combine(fixture.Root, "reports"));
        }

        private LaunchExecutionContext Launch(TestWorkspace workspace, string test)
        {
            return new LaunchExecutionContext(workspace, "run01", test, "worker01", Path.Combine(fixture.Root, "outputs", test));
        }

        private static TestExecutionContext Parse(LaunchExecutionContext launch)
        {
            var user = launch.UserArguments(new[] { "--", "--frontend" });
            var result = TestExecutionContext.Parse(launch.EnvironmentVariables(), user);
            Assert.NotNull(result);
            return result;
        }

        private static void WriteToken(TestExecutionContext context)
        {
            Directory.CreateDirectory(context.LogRoot);
            File.WriteAllText(context.MainLogPath, context.LaunchId);
        }

        /// <summary>Every workspace/launch is disposed before deleting this test's tiny physical inputs and output evidence.</summary>
        public void Dispose()
        {
            fixture.Dispose();
        }
    }
}
