namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Text;

    /// <summary>Streams both output channels to a durable log, applies child-only environment and contains timeouts.</summary>
    public static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, IEnumerable<string> arguments, string workingDirectory, int timeoutSeconds,
            string logFile = null, IReadOnlyDictionary<string, string> environment = null,
            string launchName = null, CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName, WorkingDirectory = workingDirectory, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            if (environment != null)
                foreach (var variable in environment) startInfo.Environment[variable.Key] = variable.Value;
            if (logFile != null) Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logFile)));
            using var writer = logFile == null ? null : new StreamWriter(logFile, false, new UTF8Encoding(false)) { AutoFlush = true };
            var result = new ProcessResult { LogFile = logFile };
            var stopwatch = Stopwatch.StartNew();
            using var stdoutDone = new ManualResetEventSlim();
            using var stderrDone = new ManualResetEventSlim();
            using var process = new Process { StartInfo = startInfo };
            var capture = new ProcessOutputCapture(result, writer, stopwatch, stdoutDone, stderrDone, launchName);
            process.OutputDataReceived += capture.StandardOutput;
            process.ErrorDataReceived += capture.StandardError;
            try
            {
                RunnerLifetime.Start(process, cancellationToken);
                Sample(process, result);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                while (!process.WaitForExit(100))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RunnerLifetime.Token.ThrowIfCancellationRequested();
                    Sample(process, result);
                    if (stopwatch.Elapsed.TotalSeconds < timeoutSeconds) continue;
                    result.TimedOut = true;
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(5000)) throw new IOException("timed-out process tree could not be stopped");
                    break;
                }
                Sample(process, result);
                result.ExitCode = result.TimedOut ? -1 : process.ExitCode;
                if (!stdoutDone.Wait(5000) || !stderrDone.Wait(5000))
                {
                    result.ExitCode = -1;
                    capture.Append("ERROR: output capture did not finish; a descendant may still own the pipes", true);
                }
            }
            catch (Exception error)
            {
                result.ExitCode = -1;
                capture.Append("ERROR: process infrastructure: " + error.Message, true);
                try
                {
                    if (!process.HasExited) { process.Kill(true); process.WaitForExit(5000); }
                    stdoutDone.Wait(5000);
                    stderrDone.Wait(5000);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
            finally { RunnerLifetime.Remove(process); }
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            cancellationToken.ThrowIfCancellationRequested();
            RunnerLifetime.Token.ThrowIfCancellationRequested();
            return result;
        }

        private static void Sample(Process process, ProcessResult result)
        {
            try
            {
                process.Refresh();
                result.CpuSeconds = Math.Max(result.CpuSeconds ?? 0, process.TotalProcessorTime.TotalSeconds);
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            try
            {
                var memory = Math.Max(process.PeakWorkingSet64, process.WorkingSet64);
                if (memory > 0) result.PeakMemoryMb = Math.Max(result.PeakMemoryMb ?? 0, memory / 1048576.0);
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
        }
    }
}
