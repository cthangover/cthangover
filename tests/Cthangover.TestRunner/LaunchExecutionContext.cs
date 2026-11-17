namespace Cthangover.TestRunner
{
    /// <summary>Claims one process output namespace before bootstrap and preserves ordered component state separately.</summary>
    public sealed class LaunchExecutionContext : IDisposable
    {
        private readonly TestWorkspace workspace;
        private readonly FileStream claim;
        private readonly LaunchExecutionReceipt receipt;
        private readonly PendingProcessClaim processClaim;
        private bool terminalSeen;

        /// <summary>Allocates fresh attempt/launch roots; outputParent must be outside prepared/write inputs and component DATA.</summary>
        public LaunchExecutionContext(TestWorkspace workspace, string runId, string testId, string workerId,
            string outputParent, string attemptId = "1")
        {
            this.workspace = workspace;
            ArgumentException.ThrowIfNullOrWhiteSpace(runId);
            ArgumentException.ThrowIfNullOrWhiteSpace(testId);
            ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
            if (!SafeToken(runId) || !SafeToken(workerId))
            {
                throw new ArgumentException("Run and worker identities must be safe tokens");
            }

            foreach (var character in testId)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException("Test identity cannot contain control characters");
                }
            }

            if (!SafeToken(attemptId))
            {
                throw new ArgumentException("Attempt identity must be a safe token", nameof(attemptId));
            }

            if (workspace.UserDirectory == null)
            {
                throw new InvalidOperationException("Begin a component before creating a launch");
            }

            if (!Path.IsPathRooted(outputParent))
            {
                throw new ArgumentException("Launch output must be absolute", nameof(outputParent));
            }

            outputParent = Path.GetFullPath(outputParent);
            if (WorkspaceOwner.IsWithin(outputParent, workspace.Root)
                || WorkspaceOwner.IsWithin(outputParent, workspace.UserDirectory))
            {
                throw new IOException("Launch output cannot be inside project inputs or component state");
            }

            PreparedInputFiles.RejectLink(outputParent);
            var launchId = Guid.NewGuid().ToString("N");
            var directory = Path.Combine(outputParent, "attempt_" + attemptId, launchId);
            Directory.CreateDirectory(directory);
            claim = new FileStream(Path.Combine(directory, "runner.owner"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            Metrics = new LaunchExecutionMetrics
            {
                RunId = runId,
                TestId = testId,
                AttemptId = attemptId,
                LaunchId = launchId,
                WorkerId = workerId,
                PreparedInputId = workspace.Inputs.PreparedInputId,
                SourceFingerprint = workspace.Inputs.SourceFingerprint,
                LeaseId = workspace.Metrics.LeaseId,
                WorkspaceMode = workspace.Metrics.WorkspaceMode,
                ProjectRoot = workspace.Root,
                DataDirectory = workspace.UserDirectory,
                OutputDirectory = directory,
                TemporaryDirectory = Path.Combine(directory, "temp"),
                LogsDirectory = Path.Combine(directory, "logs")
            };
            foreach (var folder in new[] { "logs", "temp", "mod-tests", "sandbox", "screens", "outputs" })
            {
                Directory.CreateDirectory(Path.Combine(directory, folder));
            }

            receipt = new LaunchExecutionReceipt
            {
                RunId = runId,
                TestId = testId,
                AttemptId = attemptId,
                LaunchId = launchId,
                NativeLogPath = Path.Combine(Metrics.LogsDirectory, "godot.log"),
                Execution = Metrics
            };
            WriteReceipt();
            try
            {
                processClaim = new PendingProcessClaim(workspace.Inputs.Root, workspace.Root, launchId, runId,
                    workspace.Inputs.PreparedInputId);
            }
            catch
            {
                claim.Dispose();
                throw;
            }
        }

        /// <summary>Exact child identities, roots and copy counters; result parsing remains the parent runner's responsibility.</summary>
        public LaunchExecutionMetrics Metrics
        {
            get;
        }

        /// <summary>True when the observed process mutated immutable baseline/build inputs, regardless of its PASS marker.</summary>
        public bool InputViolation => receipt.ImmutableFenceChanges.Count > 0;

        /// <summary>Sets every agreed Core identity before Process.Start without changing parent process environment.</summary>
        public Dictionary<string, string> EnvironmentVariables()
        {
            var result = workspace.EnvironmentVariables();
            result["CTHANGOVER_TEST_RUN_ID"] = Metrics.RunId;
            result["CTHANGOVER_TEST_ID"] = Metrics.TestId;
            result["CTHANGOVER_TEST_ATTEMPT_ID"] = Metrics.AttemptId;
            result["CTHANGOVER_TEST_LAUNCH_ID"] = Metrics.LaunchId;
            result["CTHANGOVER_TEST_WORKER_ID"] = Metrics.WorkerId;
            result["CTHANGOVER_TEST_PREPARED_REVISION"] = Metrics.SourceFingerprint;
            result["CTHANGOVER_TEST_INPUT_IDENTITY"] = Metrics.PreparedInputId;
            result["CTHANGOVER_TEST_LEASE_ID"] = Metrics.LeaseId;
            result["CTHANGOVER_TEST_ACCESS"] = Metrics.WorkspaceMode == "persistent-write" ? "write"
                : Metrics.WorkspaceMode == "prepared-output" ? "output" : "read";
            result["CTHANGOVER_TEST_ARTIFACTS"] = Metrics.OutputDirectory;
            result["CTHANGOVER_TEST_LOGS"] = Metrics.LogsDirectory;
            result["TMP"] = Metrics.TemporaryDirectory;
            result["TEMP"] = Metrics.TemporaryDirectory;
            result["TMPDIR"] = Metrics.TemporaryDirectory;
            return result;
        }

        /// <summary>Records actual native arguments before the separator, including the independent engine log route.</summary>
        public List<string> EngineArguments(string scene, bool headless)
        {
            var arguments = new List<string>();
            if (headless)
            {
                arguments.Add("--headless");
            }

            arguments.Add("--log-file");
            arguments.Add(receipt.NativeLogPath);
            arguments.Add("--path");
            arguments.Add(workspace.Root);
            arguments.Add(scene);
            receipt.EngineArguments = arguments.ToArray();
            WriteReceipt();
            return arguments;
        }

        /// <summary>Moves declared project-artifact argument scopes to unique external launch outputs.</summary>
        public List<string> UserArguments(IEnumerable<string> arguments)
        {
            var result = new List<string> { "--" };
            foreach (var argument in arguments)
            {
                if (argument == "--")
                {
                    continue;
                }

                var separator = argument.IndexOf('=');
                var value = argument.Substring(separator + 1).Replace('\\', '/');
                const string prefix = "res://tests/artifacts/";
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var relative = value.Substring(prefix.Length).TrimEnd('/');
                    var output = PreparedInputFiles.SafePath(Path.Combine(Metrics.OutputDirectory, "outputs"), relative);
                    Directory.CreateDirectory(output);
                    result.Add(argument.Substring(0, separator + 1) + output);
                }
                else
                {
                    result.Add(argument);
                }
            }

            return result;
        }

        /// <summary>Atomically publishes the parent's actual exit/telemetry; this record alone never claims a passing test.</summary>
        public void WriteTerminal(ProcessResult result)
        {
            terminalSeen = true;
            if (!result.CleanupSafe)
            {
                workspace.PreserveUnconfirmedProcess(result, Metrics.LaunchId);
            }
            processClaim.OnCompleted(result);
            receipt.Status = result.Cancelled ? "cancelled" : result.TimedOut ? "timed_out"
                : result.ProcessId == null ? "failed_to_start" : "exited";
            receipt.ProcessId = result.ProcessId;
            receipt.ExitCode = result.ObservedExitCode;
            receipt.ObservedExitCode = result.ObservedExitCode;
            receipt.ExitConfirmed = result.ExitConfirmed;
            receipt.ProcessCreatedAt = result.ProcessCreatedAt;
            receipt.StdoutCompleted = result.StdoutCompleted;
            receipt.StderrCompleted = result.StderrCompleted;
            receipt.CleanupSafe = result.CleanupSafe;
            receipt.TerminationState = result.TerminationState;
            receipt.ElapsedSeconds = result.Duration.TotalSeconds;
            receipt.CpuSeconds = result.CpuSeconds;
            receipt.PeakMemoryMb = result.PeakMemoryMb;
            if (!result.CleanupSafe)
            {
                receipt.Status = result.TerminationState;
                receipt.FenceClassification = "not-checked";
                WriteReceipt();
                return;
            }
            if (!result.Started)
            {
                WriteReceipt();
                return;
            }
            var changes = workspace.VerifyUnchanged();
            var immutable = Metrics.WorkspaceMode == "persistent-write"
                ? workspace.Inputs.VerifyUnchanged() : changes;
            WriteFence(changes, immutable);
        }

        /// <summary>Separates expected writable-lane deltas from forbidden mutation of sealed baseline/build inputs.</summary>
        public void WriteFence(IReadOnlyList<string> changes, IReadOnlyList<string> immutableChanges)
        {
            receipt.FenceChanges = changes.ToList();
            receipt.ImmutableFenceChanges = immutableChanges.ToList();
            receipt.FenceClassification = immutableChanges.Count > 0 ? "immutable-violation"
                : Metrics.WorkspaceMode == "persistent-write" ? "write-lane-delta" : "immutable-unchanged";
            WriteReceipt();
        }

        /// <summary>Releases the launch claim after the parent drains child output; all evidence remains retained.</summary>
        public void Dispose()
        {
            if (!terminalSeen)
            {
                processClaim.OnCompleted(new ProcessResult());
            }
            claim.Dispose();
        }

        private void WriteReceipt()
        {
            PreparedInputJson.Write(Path.Combine(Metrics.OutputDirectory, "launch.json"), receipt);
        }

        private static bool SafeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value is "." or "..")
            {
                return false;
            }

            foreach (var character in value)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '_' && character != '-' && character != '.')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
