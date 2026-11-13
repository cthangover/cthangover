namespace Cthangover.Tests.Testing
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Cthangover.TestRunner;

    /// <summary>Real pipe capture distinguishes stdout driver categories from stderr/text and propagates nullable process telemetry.</summary>
    [Collection("runner-infrastructure")]
    public class ProcessRunnerTests
    {
        [Fact]
        public void CapturesDriverTimestampLineCountAndRootResources()
        {
            var windows = OperatingSystem.IsWindows();
            var command = windows
                ? "echo [MODTEST] stderr 1>&2 & echo prefix [TEST] text & echo [date] [TEST] first & echo [TEST_RESULT] name=probe passed=1 failed=0"
                : "printf '[MODTEST] stderr\\n' >&2; printf 'prefix [TEST] text\\n'; sleep 0.15; printf '[date] \\033[37m[TEST]\\033[0m first\\n'; printf '[TEST_RESULT] name=probe passed=1 failed=0\\n'";
            var process = ProcessRunner.Run(windows ? "cmd.exe" : "/bin/sh", windows ? new[] { "/d", "/c", command } : new[] { "-c", command }, Environment.CurrentDirectory, 10);
            Assert.Equal(0, process.ExitCode);
            Assert.False(process.TimedOut);
            Assert.Equal(4, process.LogLines);
            Assert.NotNull(process.BootSeconds);
            Assert.InRange(process.BootSeconds.Value, windows ? 0 : 0.1, process.Duration.TotalSeconds);
            if (process.CpuSeconds.HasValue) Assert.True(process.CpuSeconds.Value >= 0);
            if (process.PeakMemoryMb.HasValue) Assert.True(process.PeakMemoryMb.Value > 0);
            var result = ResultParser.Evaluate("probe", "integration", process, new ResultContract { Name = "probe" });
            Assert.True(result.Passed, result.Note);
            Assert.Equal(process.BootSeconds, result.BootSeconds);
            Assert.Equal(process.CpuSeconds, result.CpuSeconds);
            Assert.Equal(process.PeakMemoryMb, result.PeakMemoryMb);
            Assert.Equal(process.LogLines, result.LogLines);
        }

        [Fact]
        public void StderrDriverCategoryDoesNotCreateABootTimestamp()
        {
            var windows = OperatingSystem.IsWindows();
            var command = windows ? "echo [TEST] stderr 1>&2" : "printf '[TEST] stderr\\n' >&2";
            var process = ProcessRunner.Run(windows ? "cmd.exe" : "/bin/sh", windows ? new[] { "/d", "/c", command } : new[] { "-c", command }, Environment.CurrentDirectory, 10);
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(1, process.LogLines);
            Assert.Null(process.BootSeconds);
        }

        [Fact]
        public void CancellationDrainsTheChildBeforeReturning()
        {
            var windows = OperatingSystem.IsWindows();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            var watch = Stopwatch.StartNew();
            Assert.Throws<OperationCanceledException>(() => ProcessRunner.Run(windows ? "cmd.exe" : "/bin/sh",
                windows ? new[] { "/d", "/c", "ping -n 30 127.0.0.1 >nul" } : new[] { "-c", "sleep 30 & wait" },
                Environment.CurrentDirectory, 30, cancellationToken: cancellation.Token));
            Assert.InRange(watch.Elapsed.TotalSeconds, 0, 5);
        }
    }
}
