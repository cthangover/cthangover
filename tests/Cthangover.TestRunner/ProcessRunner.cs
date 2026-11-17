namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Text;

    /// <summary>Streams both output channels to a durable log, applies child-only environment and contains timeouts.</summary>
    public static class ProcessRunner
    {
        /// <summary>
        /// Supervises one child and its pipes with child-only environment, timeout/cancellation tree cleanup and measured telemetry.
        /// </summary>
        /// <param name="onCompleted">
        /// Publishes an owner's durable outcome even when cancellation will be rethrown. Failure retains conservative ownership.
        /// </param>
        public static ProcessResult Run(string fileName, IEnumerable<string> arguments, string workingDirectory, int timeoutSeconds,
            string logFile = null, IReadOnlyDictionary<string, string> environment = null,
            string launchName = null, CancellationToken cancellationToken = default, LaunchExecutionContext launchContext = null,
            Action<ProcessResult> onCompleted = null)
        {
            var result = new ProcessResult
            {
                LogFile = logFile,
                ExitCode = -1
            };
            var stopwatch = Stopwatch.StartNew();
            StreamWriter writer = null;
            StreamWriter stdoutWriter = null;
            StreamWriter stderrWriter = null;
            ManualResetEventSlim stdoutDone = null;
            ManualResetEventSlim stderrDone = null;
            Process process = null;
            ProcessOutputCapture capture = null;
            var failed = false;
            try
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
                {
                    startInfo.ArgumentList.Add(argument);
                }

                if (environment != null)
                {
                    foreach (var variable in environment)
                    {
                        startInfo.Environment[variable.Key] = variable.Value;
                    }
                }

                if (logFile != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logFile)));
                }

                var logDirectory = logFile == null ? null : Path.GetDirectoryName(Path.GetFullPath(logFile));
                writer = OpenWriter(logFile);
                stdoutWriter = OpenWriter(logDirectory == null ? null : Path.Combine(logDirectory, "stdout.log"));
                stderrWriter = OpenWriter(logDirectory == null ? null : Path.Combine(logDirectory, "stderr.log"));
                stdoutDone = new ManualResetEventSlim();
                stderrDone = new ManualResetEventSlim();
                process = new Process
                {
                    StartInfo = startInfo
                };
                capture = new ProcessOutputCapture(result, writer, stopwatch, stdoutDone, stderrDone, launchName,
                    stdoutWriter, stderrWriter);
                process.OutputDataReceived += capture.StandardOutput;
                process.ErrorDataReceived += capture.StandardError;
                try
                {
                    RunnerLifetime.Start(process, cancellationToken);
                    result.ProcessId = process.Id;
                    ObserveCreation(process, result);
                    Sample(process, result);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    while (!process.WaitForExit(100))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        RunnerLifetime.Token.ThrowIfCancellationRequested();
                        Sample(process, result);
                        if (stopwatch.Elapsed.TotalSeconds >= timeoutSeconds)
                        {
                            result.TimedOut = true;
                            break;
                        }
                    }
                }
                catch (Exception error)
                {
                    failed = true;
                    AppendFailure(capture, result, "ERROR: process infrastructure: " + error.Message);
                }
            }
            catch (Exception error)
            {
                failed = true;
                AppendFailure(capture, result, "ERROR: process setup: " + error.Message);
            }
            finally
            {
                try
                {
                    if (result.Started)
                    {
                        if (!ObserveExit(process, result))
                        {
                            Stop(process, result, capture);
                        }

                        Sample(process, result);
                        var drain = Stopwatch.StartNew();
                        result.StdoutCompleted = stdoutDone.Wait(5000);
                        var remaining = Math.Max(0, 5000 - (int)drain.ElapsedMilliseconds);
                        result.StderrCompleted = stderrDone.Wait(remaining);
                        if (!result.ExitConfirmed)
                        {
                            AppendFailure(capture, result, "ERROR: process exit remains unproven after bounded cleanup");
                        }

                        if (!result.PipesCompleted)
                        {
                            AppendFailure(capture, result,
                                "ERROR: output capture did not finish; a descendant may still own the pipes");
                        }
                    }
                }
                catch (Exception error)
                {
                    failed = true;
                    AppendFailure(capture, result, "ERROR: process observation/cleanup: " + error.Message);
                }
                finally
                {
                    stopwatch.Stop();
                    result.Duration = stopwatch.Elapsed;
                    result.Cancelled = cancellationToken.IsCancellationRequested || RunnerLifetime.Token.IsCancellationRequested;
                    if (!failed && !result.TimedOut && !result.Cancelled && result.ExitConfirmed && result.PipesCompleted)
                    {
                        result.ExitCode = result.ObservedExitCode.Value;
                    }

                    if (!result.CleanupSafe)
                    {
                        RunnerLifetime.Complete(process, false, stdoutDone, stderrDone, writer, stdoutWriter, stderrWriter);
                    }

                    var completionRecorded = false;
                    try
                    {
                        // Durable containment precedes both normal release and any cancellation propagation.
                        try
                        {
                            launchContext?.WriteTerminal(result);
                        }
                        finally
                        {
                            onCompleted?.Invoke(result);
                        }

                        completionRecorded = true;
                    }
                    finally
                    {
                        var releaseOwnership = result.CleanupSafe && completionRecorded;
                        RunnerLifetime.Complete(process, releaseOwnership,
                            stdoutDone, stderrDone, writer, stdoutWriter, stderrWriter);
                        if (releaseOwnership)
                        {
                            Release(process, stdoutDone, stderrDone, writer, stdoutWriter, stderrWriter);
                        }
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            RunnerLifetime.Token.ThrowIfCancellationRequested();
            return result;
        }

        private static void AppendFailure(ProcessOutputCapture capture, ProcessResult result, string message)
        {
            lock (result.Output)
            {
                if (capture == null)
                {
                    result.Output.Add(message);
                    result.LogLines++;
                    return;
                }

                try
                {
                    capture.Append(message, true);
                }
                catch (Exception)
                {
                    // The failed outcome survives a broken evidence writer; terminal containment still has to run.
                }
            }
        }

        private static void Release(params IDisposable[] resources)
        {
            var failures = new List<Exception>();
            foreach (var resource in resources)
            {
                try
                {
                    resource?.Dispose();
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("Completed process resource release failed", failures);
            }
        }

        private static StreamWriter OpenWriter(string path)
        {
            return path == null ? null : new StreamWriter(path, false, new UTF8Encoding(false))
            {
                AutoFlush = true
            };
        }

        private static void ObserveCreation(Process process, ProcessResult result)
        {
            try
            {
                result.ProcessCreatedAt = new DateTimeOffset(process.StartTime.ToUniversalTime());
            }
            catch (Exception error) when (error is InvalidOperationException
                or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // An unavailable OS creation identity stays null; parent observation time is not a substitute.
            }
        }

        private static bool ObserveExit(Process process, ProcessResult result)
        {
            try
            {
                if (process.HasExited)
                {
                    result.ObservedExitCode = process.ExitCode;
                    result.ExitConfirmed = true;
                }
            }
            catch (Exception error) when (error is InvalidOperationException
                or System.ComponentModel.Win32Exception or NotSupportedException)
            {
            }

            return result.ExitConfirmed;
        }

        private static void Stop(Process process, ProcessResult result, ProcessOutputCapture capture)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                if (process.WaitForExit(5000))
                {
                    ObserveExit(process, result);
                }
            }
            catch (Exception error)
            {
                AppendFailure(capture, result, "ERROR: bounded process cleanup: " + error.Message);
            }

            ObserveExit(process, result);
        }

        private static void Sample(Process process, ProcessResult result)
        {
            try
            {
                process.Refresh();
                result.CpuSeconds = Math.Max(result.CpuSeconds ?? 0, process.TotalProcessorTime.TotalSeconds);
            }
            catch (Exception error) when (error is InvalidOperationException
                or System.ComponentModel.Win32Exception or NotSupportedException)
            {
            }
            try
            {
                var memory = Math.Max(process.PeakWorkingSet64, process.WorkingSet64);
                if (memory > 0)
                    result.PeakMemoryMb = Math.Max(result.PeakMemoryMb ?? 0, memory / 1048576.0);
            }
            catch (Exception error) when (error is InvalidOperationException
                or System.ComponentModel.Win32Exception or NotSupportedException)
            {
            }
        }
    }
}
