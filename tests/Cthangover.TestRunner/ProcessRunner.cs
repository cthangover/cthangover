namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Text;

    /// <summary>Streams both output channels to a durable log, applies child-only environment and contains timeouts.</summary>
    public static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, IEnumerable<string> arguments, string workingDirectory, int timeoutSeconds,
            string logFile = null, IReadOnlyDictionary<string, string> environment = null)
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
            process.OutputDataReceived += (_, e) => Append(result, writer, stdoutDone, e.Data);
            process.ErrorDataReceived += (_, e) => Append(result, writer, stderrDone, e.Data);
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(checked(timeoutSeconds * 1000)))
                {
                    result.TimedOut = true;
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(5000)) throw new IOException("timed-out process tree could not be stopped");
                }
                result.ExitCode = result.TimedOut ? -1 : process.ExitCode;
                if (!stdoutDone.Wait(5000) || !stderrDone.Wait(5000))
                {
                    result.ExitCode = -1;
                    Append(result, writer, stdoutDone, "ERROR: output capture did not finish; a descendant may still own the pipes");
                }
            }
            catch (Exception error)
            {
                result.ExitCode = -1;
                Append(result, writer, stdoutDone, "ERROR: process infrastructure: " + error.Message);
                try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(5000); } } catch { }
            }
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }

        private static void Append(ProcessResult result, StreamWriter writer, ManualResetEventSlim done, string line)
        {
            if (line == null) { done.Set(); return; }
            lock (result.Output)
            {
                result.Output.Add(line);
                writer?.WriteLine(line);
            }
            if (line.TrimStart().StartsWith("[", StringComparison.Ordinal) && (line.Contains("[TEST_RESULT]", StringComparison.Ordinal) || line.Contains("[FAIL]", StringComparison.Ordinal)))
                Console.WriteLine("  " + line);
        }
    }
}
