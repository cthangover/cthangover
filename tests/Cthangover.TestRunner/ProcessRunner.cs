using System.Diagnostics;
using System.Text;

namespace Cthangover.TestRunner
{
    /// <summary>
    /// Cross-platform process launcher for the test runner. Uses
    /// <see cref="ProcessStartInfo.ArgumentList"/> so paths and arguments with
    /// spaces survive on every OS, reads stdout/stderr asynchronously in UTF-8
    /// (Godot logs are UTF-8) and kills the whole process tree on timeout, so a
    /// hung Godot can never stall the global run.
    /// </summary>
    public static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, IEnumerable<string> arguments, string workingDirectory, int timeoutSeconds)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            var result = new ProcessResult();
            var stopwatch = Stopwatch.StartNew();

            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => Append(result, e.Data);
            process.ErrorDataReceived += (_, e) => Append(result, e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutSeconds * 1000))
            {
                result.TimedOut = true;
                try { process.Kill(entireProcessTree: true); } catch { }
                process.WaitForExit(5000);
            }
            else
            {
                process.WaitForExit();
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.ExitCode = result.TimedOut ? -1 : process.ExitCode;
            return result;
        }

        private static void Append(ProcessResult result, string line)
        {
            if (line == null)
                return;
            lock (result.Output)
                result.Output.Add(line);
        }
    }
}
