namespace Cthangover.ProcessContainment
{
    using System.Diagnostics;
    using System.ComponentModel;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text.Json;
    using Cthangover.TestRunner;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Contains the intentionally unsafe runner guard in a dedicated supervisor with one identified pipe-holding descendant.
    /// </summary>
    public static class Program
    {
        private static readonly ManualResetEventSlim changed = new();

        /// <summary>Runs supervisor/root/descendant roles; every wait has a finite budget and readiness is filesystem-signalled.</summary>
        public static int Main(string[] arguments)
        {
            var mode = arguments[0];
            var root = arguments[1];
            return mode == "child" ? Child(root) : mode == "root" ? Root(root) : Supervise(root);
        }

        private static int Child(string root)
        {
            using var current = Process.GetCurrentProcess();
            Write(Path.Combine(root, "child-ready.json"), new Dictionary<string, object>
            {
                ["pid"] = current.Id,
                ["createdAt"] = new DateTimeOffset(current.StartTime.ToUniversalTime())
            });
            WaitFile(Path.Combine(root, "child-release"), 18);
            return 0;
        }

        private static int Root(string root)
        {
            using var child = Process.Start(StartInfo("child", root, false));
            Require(child != null, "Controlled descendant did not start.");
            WaitFile(Path.Combine(root, "child-ready.json"), 5);
            return 0;
        }

        private static int Supervise(string root)
        {
            var evidence = new Dictionary<string, object>();
            Process child = null;
            SafeProcessHandle childHandle = null;
            PreparedInputLease inputs = null;
            TestWorkspace workspace = null;
            LaunchExecutionContext launch = null;
            var successful = false;
            var childExited = false;
            int? childExitCode = null;
            var cache = Path.Combine(root, "cache");
            var source = Path.Combine(root, "source");
            try
            {
                var temporary = Path.Combine(root, "temp");
                Require(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                    == Path.GetFullPath(temporary), "Supervisor must start with its existing fixture TEMP before CLR startup.");
                Directory.CreateDirectory(Path.Combine(source, "mods", "probe"));
                File.WriteAllText(Path.Combine(source, "project.godot"), "config_version=5\n");
                File.WriteAllText(Path.Combine(source, "mods", "probe", "manifest.json"), "{\"id\":\"probe\"}");
                File.WriteAllText(Path.Combine(source, "mods", "probe", "data.json"), "{\"value\":1}");
                inputs = PreparedInputStore.Acquire(source, cache);
                workspace = new TestWorkspace(inputs, "containment-" + Guid.NewGuid().ToString("N"), true, writeSlots: 1);
                workspace.Begin("controlled");
                var runRoot = workspace.RunRoot;
                var dataRoot = workspace.UserDirectory;
                Require(Path.GetRelativePath(temporary, runRoot).StartsWith("cthangover-tests" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal), "Actual run root escaped owned fixture TEMP.");
                var dataRelative = Path.GetRelativePath(temporary, dataRoot);
                Require(!Path.IsPathRooted(dataRelative) && dataRelative != ".."
                    && !dataRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal),
                    "Actual DATA escaped the fixture TEMP.");
                var dataSentinel = Path.Combine(dataRoot, "unsafe-owner.txt");
                File.WriteAllText(dataSentinel, "DATA_UNSAFE_SENTINEL");
                var dataBefore = DataInventory(dataRoot);
                launch = new LaunchExecutionContext(workspace, "containment", "unfinished_pipes", "worker",
                    Path.Combine(root, "outputs"));
                evidence["sourceRoot"] = source;
                evidence["cacheRoot"] = cache;
                evidence["preparedRoot"] = inputs.Root;
                evidence["laneRoot"] = workspace.Root;
                evidence["runRoot"] = workspace.RunRoot;
                evidence["dataDirectory"] = dataRoot;
                evidence["dataSentinelPath"] = dataSentinel;
                evidence["dataSentinelSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dataSentinel))).ToLowerInvariant();
                evidence["dataBefore"] = dataBefore;
                evidence["temporaryRoot"] = temporary;
                evidence["inputLeaseId"] = inputs.LeaseId;
                using (var reader = PreparedInputStore.Acquire(source, cache))
                {
                    Require(reader.PreparedInputId == inputs.PreparedInputId && reader.SourceFingerprint == inputs.SourceFingerprint
                        && reader.LeaseId != inputs.LeaseId, "Live pending parent did not admit an independent same-revision reader.");
                    Require(reader.Metrics.FilesCopied == 0 && reader.Metrics.BytesCopied == 0
                        && reader.Metrics.FullProjectCopies == 0 && reader.VerifyUnchanged().Count == 0,
                        "Live pending reader changed or recopied sealed inputs.");
                    evidence["livePendingReadAdmitted"] = true;
                    evidence["livePendingReadCopiedFiles"] = reader.Metrics.FilesCopied;
                    evidence["livePendingReadLeaseId"] = reader.LeaseId;
                }

                using (var secondLane = PersistentWriteLane.Acquire(inputs, 2))
                {
                    Require(secondLane.Root != workspace.Root, "Live pending lane0 prevented admission to independent free lane1.");
                    Require(File.ReadAllText(Path.Combine(secondLane.Root, "mods", "probe", "data.json")) == "{\"value\":1}"
                        && inputs.VerifyUnchanged().Count == 0, "Second physical lane did not preserve the source/baseline payload.");
                    evidence["livePendingSecondLaneAdmitted"] = true;
                    evidence["livePendingSecondLaneRoot"] = secondLane.Root;
                    evidence["livePendingSecondLaneMetrics"] = secondLane.Metrics;
                }

                var start = StartInfo("root", root, true);
                var result = ProcessRunner.Run(start.FileName, start.ArgumentList, root, 10,
                    logFile: Path.Combine(launch.Metrics.OutputDirectory, "process.log"), launchContext: launch);
                evidence["process"] = result;
                evidence["launchOutput"] = launch.Metrics.OutputDirectory;
                Require(result.Started && result.ProcessId.HasValue && result.ProcessCreatedAt.HasValue,
                    "The actual root PID/creation identity was not captured.");
                Require(result.ExitConfirmed && result.ObservedExitCode == 0, "Actual root exit0 was not observed.");
                Require(!result.StdoutCompleted && !result.StderrCompleted && !result.PipesCompleted && !result.CleanupSafe
                    && result.TerminationState == "unfinished-pipes", "Open inherited pipes were incorrectly considered safe.");
                Require(!result.TimedOut && result.Duration.TotalSeconds < 12, "Unfinished pipes exceeded the shared drain budget.");
                using var identity = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "child-ready.json")));
                var pid = identity.RootElement.GetProperty("pid").GetInt32();
                var createdAt = identity.RootElement.GetProperty("createdAt").GetDateTimeOffset();
                child = Process.GetProcessById(pid);
                childHandle = child.SafeHandle;
                Require(new DateTimeOffset(child.StartTime.ToUniversalTime()) == createdAt && !child.HasExited,
                    "The independently identified descendant was no longer alive at quarantine assertions.");
                evidence["childPid"] = pid;
                evidence["childCreatedAt"] = createdAt;
                launch.Dispose();
                launch = null;
                workspace.Dispose();
                workspace = null;
                Require(Directory.Exists(runRoot) && File.ReadAllText(dataSentinel) == "DATA_UNSAFE_SENTINEL",
                    "Unsafe workspace Dispose removed actual run/DATA state.");
                var afterDispose = DataInventory(dataRoot);
                Require(JsonSerializer.Serialize(dataBefore) == JsonSerializer.Serialize(afterDispose),
                    "Dispose changed owned DATA inventory.");
                evidence["dataPreservedAfterDispose"] = true;
                evidence["dataAfterDispose"] = afterDispose;
                Require(!child.HasExited, "Controlled descendant exited before targeted DATA retention.");
                var removedRuns = WorkspaceRetention.Prune(temporary, 0, 0, 0, dataHomes: Array.Empty<string>());
                Require(removedRuns.Count == 0 && Directory.Exists(runRoot)
                    && File.ReadAllText(dataSentinel) == "DATA_UNSAFE_SENTINEL",
                    "Targeted owned-temp retention removed unsafe run/DATA state.");
                evidence["runDataPreservedAfterDisposeAndRetention"] = true;
                var afterRetention = DataInventory(dataRoot);
                Require(JsonSerializer.Serialize(dataBefore) == JsonSerializer.Serialize(afterRetention),
                    "Retention changed owned DATA inventory.");
                evidence["dataPreservedAfterRetention"] = true;
                evidence["dataAfterRetention"] = afterRetention;
                evidence["workspaceRetentionRemoved"] = removedRuns;
                evidence["dataSentinelAfter"] = File.ReadAllText(dataSentinel);
                try
                {
                    using var lane = PersistentWriteLane.Acquire(inputs, 1);
                    throw new InvalidOperationException("Unsafe write lane was admitted.");
                }
                catch (PreparedInputRefusal refusal)
                {
                    Require(refusal.Reason == "write-lane-process-unconfirmed", "Unexpected write lane refusal: " + refusal.Reason);
                    evidence["laneRefusal"] = refusal.Reason;
                }

                inputs.Dispose();
                inputs = null;
                RefuseAdmission(source, cache, false, evidence);
                RefuseAdmission(source, cache, true, evidence);
                var decisions = PreparedInputStore.Prune(cache, 0, 0, 0);
                Require(decisions.Count == 1 && decisions[0].Decision == "retained-active"
                    && decisions[0].Reason == "unconfirmed-process-owner", "Retention did not preserve unsafe durable ownership.");
                evidence["retention"] = decisions;
                Require(!child.HasExited, "Controlled descendant exited before durable admission assertions completed.");
                var refused = ProcessRunner.Run(start.FileName, new[] { "--version" }, root, 2);
                Require(!refused.Started, "The dedicated unsafe supervisor admitted a later process.");
                evidence["nextStart"] = refused;
                Require(File.ReadAllText(Path.Combine(source, "mods", "probe", "data.json")) == "{\"value\":1}",
                    "Original fixture source changed.");
                successful = true;
            }
            catch (Exception error)
            {
                evidence["error"] = error.ToString();
            }
            finally
            {
                try
                {
                    File.WriteAllText(Path.Combine(root, "child-release"), "release");
                    if (child == null && File.Exists(Path.Combine(root, "child-ready.json")))
                    {
                        using var identity = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "child-ready.json")));
                        child = Process.GetProcessById(identity.RootElement.GetProperty("pid").GetInt32());
                        childHandle = child.SafeHandle;
                        Require(new DateTimeOffset(child.StartTime.ToUniversalTime())
                            == identity.RootElement.GetProperty("createdAt").GetDateTimeOffset(), "Descendant PID identity changed.");
                    }

                    if (child != null && child.WaitForExit(3000))
                    {
                        childExitCode = ObserveExitCode(childHandle);
                        childExited = true;
                        Require(childExitCode == 0, "Controlled descendant returned an actual nonzero OS exit code.");
                    }
                }
                catch (Exception error)
                {
                    successful = false;
                    evidence["cleanupError"] = error.ToString();
                }
                finally
                {
                    launch?.Dispose();
                    workspace?.Dispose();
                    inputs?.Dispose();
                    child?.Dispose();
                    evidence["childExitConfirmed"] = childExited;
                    evidence["childObservedExitCode"] = childExitCode;
                    evidence["childExitReader"] = "GetExitCodeProcess/pinned-foreign-handle";
                    evidence["complete"] = successful && childExited;
                    Write(Path.Combine(root, "result.json"), evidence);
                }
            }

            return successful && childExited ? 0 : 1;
        }

        private static int ObserveExitCode(SafeProcessHandle handle)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("This dedicated foreign-process exit-code proof requires Windows.");
            }

            if (handle == null || handle.IsInvalid || handle.IsClosed || !GetExitCodeProcess(handle, out var code))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not read the pinned descendant OS exit code.");
            }

            return unchecked((int)code);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint code);

        private static void RefuseAdmission(string source, string cache, bool repair, Dictionary<string, object> evidence)
        {
            try
            {
                using var lease = repair ? PreparedInputStore.Repair(source, cache) : PreparedInputStore.Acquire(source, cache);
                throw new InvalidOperationException("Unsafe inputs were admitted: repair=" + repair);
            }
            catch (PreparedInputRefusal refusal)
            {
                Require(refusal.Reason == "prepared-process-unconfirmed", "Unexpected admission refusal: " + refusal.Reason);
                evidence[repair ? "repairRefusal" : "acquireRefusal"] = refusal.Reason;
            }
        }

        private static SortedDictionary<string, string> DataInventory(string directory)
        {
            var inventory = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                inventory[Path.GetRelativePath(directory, file).Replace('\\', '/')] =
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
            }

            return inventory;
        }

        private static ProcessStartInfo StartInfo(string mode, string root, bool redirect)
        {
            var start = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath,
                UseShellExecute = false,
                RedirectStandardOutput = redirect,
                RedirectStandardError = redirect
            };
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add(mode);
            start.ArgumentList.Add(root);
            return start;
        }

        private static void WaitFile(string path, int seconds)
        {
            using var watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path));
            watcher.Created += Changed;
            watcher.Changed += Changed;
            watcher.Renamed += Renamed;
            watcher.EnableRaisingEvents = true;
            var watch = Stopwatch.StartNew();
            while (!File.Exists(path))
            {
                changed.Reset();
                if (File.Exists(path))
                {
                    break;
                }

                var remaining = TimeSpan.FromSeconds(seconds) - watch.Elapsed;
                Require(remaining > TimeSpan.Zero && changed.Wait(remaining), "Controlled file gate timed out: " + path);
            }
        }

        private static void Changed(object sender, FileSystemEventArgs args)
        {
            changed.Set();
        }

        private static void Renamed(object sender, RenamedEventArgs args)
        {
            changed.Set();
        }

        private static void Write(string path, Dictionary<string, object> value)
        {
            var json = JsonSerializer.Serialize(value);
            var roundTrip = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            using var original = JsonDocument.Parse(json);
            Require(JsonElement.DeepEquals(original.RootElement, JsonSerializer.SerializeToElement(roundTrip)),
                "Process evidence JSON did not round-trip.");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, path, true);
        }

        private static void Require(bool value, string message)
        {
            if (!value)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
