namespace Cthangover.Tests.Testing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.TestRunner;

    /// <summary>Checks real child pipes, telemetry and cancellation after observed child readiness.</summary>
    [Collection("runner-infrastructure")]
    public class ProcessRunnerTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "cthangover-pipes-" + Guid.NewGuid().ToString("N"));
        private readonly CancellationTokenSource cancellation = new();
        private readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task<ProcessResult> running;

        /// <summary>Real inherited pipes retain durable source/lane quarantine after root exit and ordinary lease disposal.</summary>
        [Fact]
        public void UnfinishedInheritedPipesBlockDurableAdmissionInDedicatedSupervisor()
        {
            using var fixture = new ProcessContainmentFixture();
            using var evidence = fixture.Run();
            var record = evidence.RootElement;
            Assert.True(record.GetProperty("complete").GetBoolean());
            Assert.True(record.GetProperty("livePendingReadAdmitted").GetBoolean());
            Assert.Equal(0, record.GetProperty("livePendingReadCopiedFiles").GetInt64());
            Assert.True(record.GetProperty("livePendingSecondLaneAdmitted").GetBoolean());
            Assert.NotEqual(record.GetProperty("laneRoot").GetString(), record.GetProperty("livePendingSecondLaneRoot").GetString());
            Assert.True(record.GetProperty("runDataPreservedAfterDisposeAndRetention").GetBoolean());
            Assert.True(record.GetProperty("dataPreservedAfterDispose").GetBoolean());
            Assert.True(record.GetProperty("dataPreservedAfterRetention").GetBoolean());
            Assert.Equal(record.GetProperty("dataBefore").GetRawText(), record.GetProperty("dataAfterDispose").GetRawText());
            Assert.Equal(record.GetProperty("dataBefore").GetRawText(), record.GetProperty("dataAfterRetention").GetRawText());
            Assert.Empty(record.GetProperty("workspaceRetentionRemoved").EnumerateArray());
            Assert.Equal("DATA_UNSAFE_SENTINEL", record.GetProperty("dataSentinelAfter").GetString());
            Assert.True(record.GetProperty("childExitConfirmed").GetBoolean());
            Assert.Equal(0, record.GetProperty("childObservedExitCode").GetInt32());
            Assert.True(record.GetProperty("childPid").GetInt32() > 0);
            Assert.NotEqual(default, record.GetProperty("childCreatedAt").GetDateTimeOffset());
            Assert.Equal("prepared-process-unconfirmed", record.GetProperty("acquireRefusal").GetString());
            Assert.Equal("prepared-process-unconfirmed", record.GetProperty("repairRefusal").GetString());
            Assert.Equal("write-lane-process-unconfirmed", record.GetProperty("laneRefusal").GetString());
            var process = record.GetProperty("process");
            Assert.True(process.GetProperty("Started").GetBoolean());
            Assert.True(process.GetProperty("ProcessId").GetInt32() > 0);
            Assert.NotEqual(default, process.GetProperty("ProcessCreatedAt").GetDateTimeOffset());
            Assert.True(process.GetProperty("ExitConfirmed").GetBoolean());
            Assert.Equal(0, process.GetProperty("ObservedExitCode").GetInt32());
            Assert.False(process.GetProperty("StdoutCompleted").GetBoolean());
            Assert.False(process.GetProperty("StderrCompleted").GetBoolean());
            Assert.False(process.GetProperty("PipesCompleted").GetBoolean());
            Assert.False(process.GetProperty("CleanupSafe").GetBoolean());
            Assert.Equal("unfinished-pipes", process.GetProperty("TerminationState").GetString());
            var decision = Assert.Single(record.GetProperty("retention").EnumerateArray());
            Assert.Equal("retained-active", decision.GetProperty("Decision").GetString());
            Assert.Equal("unconfirmed-process-owner", decision.GetProperty("Reason").GetString());
            Assert.False(record.GetProperty("nextStart").GetProperty("Started").GetBoolean());
        }

        /// <summary>Preserves exact line counts and boot timing; stderr never supplies the first driver timestamp.</summary>
        [Fact]
        public void CapturesDriverTimestampLineCountAndRootResources()
        {
            var windows = OperatingSystem.IsWindows();
            var command = windows
                ? "echo [MODTEST] stderr 1>&2 & echo prefix [TEST] text & echo [date] [TEST] first & "
                    + "echo [TEST_RESULT] name=probe passed=1 failed=0"
                : "printf '[MODTEST] stderr\\n' >&2; printf 'prefix [TEST] text\\n'; sleep 0.15; "
                    + "printf '[date] \\033[37m[TEST]\\033[0m first\\n'; printf '[TEST_RESULT] name=probe passed=1 failed=0\\n'";
            var arguments = windows ? new[]
            {
                "/d",
                "/c",
                command
            } : new[]
            {
                "-c",
                command
            };
            var process = ProcessRunner.Run(windows ? "cmd.exe" : "/bin/sh", arguments, Environment.CurrentDirectory, 10);
            Assert.Equal(0, process.ExitCode);
            Assert.False(process.TimedOut);
            Assert.Equal(4, process.LogLines);
            Assert.NotNull(process.BootSeconds);
            Assert.InRange(process.BootSeconds.Value, windows ? 0 : 0.1, process.Duration.TotalSeconds);
            if (process.CpuSeconds.HasValue)
            {
                Assert.True(process.CpuSeconds.Value >= 0);
            }

            if (process.PeakMemoryMb.HasValue)
            {
                Assert.True(process.PeakMemoryMb.Value > 0);
            }

            var result = ResultParser.Evaluate("probe", "integration", process, new ResultContract { Name = "probe" });
            Assert.True(result.Passed, result.Note);
            Assert.Equal(process.BootSeconds, result.BootSeconds);
            Assert.Equal(process.CpuSeconds, result.CpuSeconds);
            Assert.Equal(process.PeakMemoryMb, result.PeakMemoryMb);
            Assert.Equal(process.LogLines, result.LogLines);
        }

        /// <summary>A stderr-only driver line is captured but cannot fabricate stdout boot readiness.</summary>
        [Fact]
        public void StderrDriverCategoryDoesNotCreateABootTimestamp()
        {
            var windows = OperatingSystem.IsWindows();
            var command = windows ? "echo [TEST] stderr 1>&2" : "printf '[TEST] stderr\\n' >&2";
            var arguments = windows ? new[]
            {
                "/d",
                "/c",
                command
            } : new[]
            {
                "-c",
                command
            };
            var process = ProcessRunner.Run(windows ? "cmd.exe" : "/bin/sh", arguments, Environment.CurrentDirectory, 10);
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(1, process.LogLines);
            Assert.Null(process.BootSeconds);
        }

        /// <summary>Cancellation begins only after a flushed child marker, then kills/drains the child within the original bound.</summary>
        [Fact]
        public async Task CancellationDrainsTheChildBeforeReturning()
        {
            Directory.CreateDirectory(root);
            using var watcher = new FileSystemWatcher(root, "stdout.log");
            watcher.Created += ReadReady;
            watcher.Changed += ReadReady;
            watcher.EnableRaisingEvents = true;
            var watch = Stopwatch.StartNew();
            running = Task.Run(RunGatedChild);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(CompleteChild);
            Assert.InRange(watch.Elapsed.TotalSeconds, 0, 5);
            Assert.Contains("PIPE_GATE_READY", File.ReadAllText(Path.Combine(root, "stdout.log")));
        }

        /// <summary>Separate stdout/stderr files preserve exact pipe identity while the compatibility log retains both lines.</summary>
        [Fact]
        public void PhysicalPipeLogsAreSeparateAndNeverInventNativeOrGameOutputs()
        {
            Directory.CreateDirectory(root);
            var windows = OperatingSystem.IsWindows();
            var command = windows ? "echo stdout_token & echo stderr_token 1>&2" : "printf 'stdout_token\\n'; printf 'stderr_token\\n' >&2";
            var arguments = windows ? new[]
            {
                "/d",
                "/c",
                command
            } : new[]
            {
                "-c",
                command
            };
            var log = Path.Combine(root, "process.log");
            var result = ProcessRunner.Run(windows ? "cmd.exe" : "/bin/sh", arguments, root, 10, logFile: log);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(2, result.LogLines);
            var stdout = File.ReadAllText(Path.Combine(root, "stdout.log"));
            var stderr = File.ReadAllText(Path.Combine(root, "stderr.log"));
            Assert.Contains("stdout_token", stdout);
            Assert.DoesNotContain("stderr_token", stdout);
            Assert.Contains("stderr_token", stderr);
            Assert.DoesNotContain("stdout_token", stderr);
            var combined = File.ReadAllText(log);
            Assert.Contains("stdout_token", combined);
            Assert.Contains("stderr_token", combined);
            Assert.False(File.Exists(Path.Combine(root, "godot.log")));
            Assert.False(File.Exists(Path.Combine(root, "cthangover.log")));
            Assert.False(File.Exists(Path.Combine(root, "cthangover_errors.log")));
        }

        private ProcessResult RunGatedChild()
        {
            var windows = OperatingSystem.IsWindows();
            var command = windows ? "echo PIPE_GATE_READY & ping -n 30 127.0.0.1 >nul" : "printf 'PIPE_GATE_READY\\n'; sleep 30 & wait";
            var arguments = windows ? new[]
            {
                "/d",
                "/c",
                command
            } : new[]
            {
                "-c",
                command
            };
            return ProcessRunner.Run(windows ? "cmd.exe" : "/bin/sh", arguments, root, 30,
                logFile: Path.Combine(root, "process.log"), cancellationToken: cancellation.Token);
        }

        private async Task CompleteChild()
        {
            await running;
        }

        private void ReadReady(object sender, FileSystemEventArgs args)
        {
            try
            {
                if (File.Exists(args.FullPath) && File.ReadAllText(args.FullPath).Contains("PIPE_GATE_READY", StringComparison.Ordinal))
                {
                    ready.TrySetResult(true);
                }
            }
            catch (IOException)
            {
                // Another notification follows the writer's flushed readiness line; no delay or polling is required.
            }
        }

        /// <summary>Cancellation and joining precede scratch removal even if readiness or an assertion failed.</summary>
        public void Dispose()
        {
            cancellation.Cancel();
            if (running != null)
            {
                try
                {
                    running.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                }
            }

            cancellation.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
