namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>Owns component state and an optional persistent write lane; immutable prepared inputs outlive run cleanup.</summary>
    public sealed class TestWorkspace : IDisposable
    {
        private readonly PreparedInputLease inputs;
        private readonly bool ownsInputs;
        private readonly string runId;
        private readonly WorkspaceOwner workspaceOwner;
        private readonly TestWorkspace owner;
        private readonly HashSet<string> declaredOutputDirectories = new(StringComparer.Ordinal);
        private PersistentWriteLane lane;
        private string dataHome;
        private bool first = true;
        private bool shared;
        private bool disposed;
        private bool unsafeProcess;
        private bool keep;

        /// <summary>Compatibility entry uses bounded persistent writes, never a fresh project clone per invocation.</summary>
        public TestWorkspace(string source, string runId, bool freshCache = false, string cacheRunId = null,
            string artifactsDirectory = null)
            : this(PreparedInputStore.Acquire(source, DefaultInputCache), runId, true, freshCache, cacheRunId,
                artifactsDirectory, 2, true)
        {
        }

        /// <summary>Borrows prepared inputs through all children; caller disposes their lease after workspace cleanup.</summary>
        public TestWorkspace(PreparedInputLease inputs, string runId, bool writable = false, bool freshCache = false,
            string cacheRunId = null, string artifactsDirectory = null, int writeSlots = 2)
            : this(inputs, runId, writable, freshCache, cacheRunId, artifactsDirectory, writeSlots, false)
        {
        }

        private TestWorkspace(PreparedInputLease inputs, string runId, bool writable, bool freshCache,
            string cacheRunId, string artifactsDirectory, int writeSlots, bool ownsInputs)
        {
            this.inputs = inputs;
            this.ownsInputs = ownsInputs;
            this.runId = runId;
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(runId);
                RunRoot = Path.Combine(Path.GetTempPath(), "cthangover-tests", runId);
                if (Path.GetFileName(RunRoot) != runId || Directory.Exists(RunRoot))
                {
                    throw new IOException("Run directory is invalid or already exists: " + RunRoot);
                }

                workspaceOwner = new WorkspaceOwner(RunRoot, runId, cacheRunId ?? runId, artifactsDirectory);
                CacheDirectory = CompilationCachePolicy.Resolve(Path.GetTempPath(), cacheRunId ?? runId, freshCache);
                if (writable)
                {
                    lane = PersistentWriteLane.Acquire(inputs, writeSlots);
                    Root = lane.Root;
                    Metrics = lane.Metrics;
                    PrepareOutputDirectories();
                }
                else
                {
                    shared = true;
                    Root = inputs.Root;
                    Metrics = ReadMetrics();
                }
            }
            catch
            {
                lane?.Dispose();
                workspaceOwner?.Dispose();
                if (ownsInputs)
                {
                    inputs.Dispose();
                }

                throw;
            }
        }

        private TestWorkspace(TestWorkspace owner, string component)
        {
            this.owner = owner;
            inputs = owner.inputs;
            runId = owner.runId;
            Root = owner.Root;
            RunRoot = owner.RunRoot;
            CacheDirectory = owner.CacheDirectory;
            Metrics = owner.Metrics;
            shared = true;
            BeginHome(component);
        }

        /// <summary>Persistent cache root, distinct from disposable state and assembly-cache subdirectories.</summary>
        public static string DefaultInputCache => Path.Combine(Path.GetTempPath(), "cthangover-tests", "cache");

        /// <summary>Actual sealed or independently writable project root.</summary>
        public string Root
        {
            get; private set;
        }

        /// <summary>Disposable run homes/temp owner; never the persistent project root.</summary>
        public string RunRoot
        {
            get;
        }

        /// <summary>Private component save/settings state, shared only by its ordered dependency chain.</summary>
        public string UserDirectory
        {
            get; private set;
        }

        /// <summary>Component temp; execution contexts allocate a different per-launch temp before bootstrap.</summary>
        public string TemporaryDirectory
        {
            get; private set;
        }

        /// <summary>Preserves run state after failure/debugging; prepared input retention is independent.</summary>
        public bool Keep
        {
            get => keep || unsafeProcess;
            set => keep = value;
        }

        internal void PreserveUnconfirmedProcess(ProcessResult result, string launchId)
        {
            var rootOwner = owner ?? this;
            rootOwner.unsafeProcess = true;
            rootOwner.keep = true;
            PreparedInputJson.Write(Path.Combine(rootOwner.RunRoot, ".process-unconfirmed.json"), new PreparedInputOwner
            {
                LeaseId = launchId,
                RunId = rootOwner.runId,
                PreparedInputId = inputs.PreparedInputId,
                Root = rootOwner.RunRoot,
                Access = result.TerminationState,
                ProcessId = result.ProcessId ?? 0,
                ProcessStartedTicks = result.ProcessCreatedAt?.UtcDateTime.Ticks ?? 0,
                Active = true,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        /// <summary>Content-keyed runtime assembly cache, retaining the existing fresh-cache policy.</summary>
        public string CacheDirectory
        {
            get;
        }

        /// <summary>Actual lane/read transfers and delta restoration work.</summary>
        public WorkspacePreparationMetrics Metrics
        {
            get; private set;
        }

        /// <summary>Borrowed input generation used by launch metadata and integrity checks.</summary>
        public PreparedInputLease Inputs => inputs;

        /// <summary>Measured component-boundary delta restoration wall time.</summary>
        public double RestoreSeconds
        {
            get; private set;
        }

        /// <summary>Actual restoration attempts; readonly components never restore inputs.</summary>
        public int RestoreCount
        {
            get; private set;
        }

        /// <summary>Switches compatibility ownership to prepared reads without any override.cfg write.</summary>
        public void ConfigureShared()
        {
            if (owner != null || !first)
            {
                throw new InvalidOperationException("Configure shared inputs before starting components");
            }

            lane?.Dispose();
            lane = null;
            Root = inputs.Root;
            Metrics = ReadMetrics();
            shared = true;
        }

        /// <summary>Shares only immutable inputs and creates a distinct native OS data home for this component.</summary>
        public TestWorkspace SharedComponent(string component)
        {
            if (!shared || owner != null)
            {
                throw new InvalidOperationException("A shared input owner is required");
            }

            return new TestWorkspace(this, component);
        }

        /// <summary>Starts fresh component state, optionally restoring an exclusively writable lane between components.</summary>
        public void Begin(string component, bool restore = true)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!first && restore && lane != null)
            {
                var started = Stopwatch.GetTimestamp();
                RestoreCount++;
                try
                {
                    lane.Restore();
                    PrepareOutputDirectories();
                }
                finally
                {
                    RestoreSeconds += Stopwatch.GetElapsedTime(started).TotalSeconds;
                }
            }

            first = false;
            BeginHome(component);
        }

        /// <summary>Returns child-only isolation; process-global environment and prepared override never change.</summary>
        public Dictionary<string, string> EnvironmentVariables()
        {
            var variables = new Dictionary<string, string>
            {
                ["CTHANGOVER_TEST_DATA"] = UserDirectory,
                ["CTHANGOVER_TEST_CACHE"] = CacheDirectory,
                ["CTHANGOVER_TEST_PROJECT"] = Root,
                ["TMP"] = TemporaryDirectory,
                ["TEMP"] = TemporaryDirectory,
                ["TMPDIR"] = TemporaryDirectory
            };
            var name = OperatingSystem.IsWindows() ? "APPDATA" : OperatingSystem.IsMacOS() ? "HOME" : "XDG_DATA_HOME";
            variables[name] = dataHome;
            variables["NUGET_PACKAGES"] = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            variables["DOTNET_CLI_HOME"] = Path.Combine(dataHome, "dotnet");
            variables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
            return variables;
        }

        /// <summary>Detects exact byte/build mutations, quarantining prepared reads before any later admission.</summary>
        public List<string> VerifyUnchanged()
        {
            if (lane == null)
            {
                return inputs.VerifyUnchanged().ToList();
            }

            var changed = PreparedInputStore.Verify(Root, inputs.Manifest, preserveScratchDirectories: false);
            changed.RemoveAll(IsDeclaredEmptyOutput);
            return changed;
        }

        /// <summary>Preserves changed/deleted authoring inputs before an independent persistent-lane component restores them.</summary>
        public List<string> CaptureChanges(string destination)
        {
            var artifacts = new List<string>();
            var removed = new List<string>();
            var directories = new List<string>();
            foreach (var relative in VerifyUnchanged())
            {
                var source = PreparedInputFiles.SafePath(Root, relative);
                if (Directory.Exists(source))
                {
                    Directory.CreateDirectory(PreparedInputFiles.SafePath(destination, relative));
                    directories.Add(relative);
                    continue;
                }
                if (!File.Exists(source))
                {
                    removed.Add(relative);
                    continue;
                }

                var target = PreparedInputFiles.SafePath(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
                artifacts.Add(target);
            }

            if (directories.Count > 0)
            {
                var path = Path.Combine(destination, "changed-directories.json");
                PreparedInputJson.Write(path, directories);
                artifacts.Add(path);
            }

            if (removed.Count > 0)
            {
                var path = Path.Combine(destination, "deleted-files.json");
                PreparedInputJson.Write(path, removed);
                artifacts.Add(path);
            }

            return artifacts;
        }

        /// <summary>Stable safe output component; test names never become traversal paths.</summary>
        public static string Key(string name)
        {
            var readable = new StringBuilder();
            foreach (var character in name)
            {
                if (readable.Length >= 48)
                {
                    break;
                }

                readable.Append(char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_');
            }

            return readable + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))
                .Substring(0, 8).ToLowerInvariant();
        }

        /// <summary>Releases the lane and removes only run-owned state; reusable prepared inputs remain available.</summary>
        public void Dispose()
        {
            if (disposed || owner != null)
            {
                return;
            }

            disposed = true;
            try
            {
                lane?.Dispose();
                workspaceOwner.ReleaseLease();
                if (!Keep && Directory.Exists(RunRoot))
                {
                    Directory.Delete(RunRoot, true);
                }
            }
            finally
            {
                workspaceOwner.Dispose();
                if (ownsInputs)
                {
                    inputs.Dispose();
                }
            }
        }

        private WorkspacePreparationMetrics ReadMetrics()
        {
            return new WorkspacePreparationMetrics
            {
                LeaseId = inputs.LeaseId,
                WorkspaceMode = "prepared-read",
                Root = inputs.Root,
                PreparedInputId = inputs.PreparedInputId,
                ReusedInputs = inputs.Metrics.ReusedInputs
            };
        }

        private void BeginHome(string component)
        {
            dataHome = Path.Combine(RunRoot, "homes", Key(component));
            if (Directory.Exists(dataHome))
            {
                throw new IOException("Component home already exists: " + dataHome);
            }

            var applicationData = OperatingSystem.IsMacOS() ? Path.Combine(dataHome, "Library", "Application Support") : dataHome;
            UserDirectory = Path.Combine(applicationData, "cthangover-tests", "data");
            Directory.CreateDirectory(Path.Combine(UserDirectory, "mods"));
            TemporaryDirectory = Path.Combine(RunRoot, "temp", Key(component));
            Directory.CreateDirectory(TemporaryDirectory);
        }

        private bool IsDeclaredEmptyOutput(string relative)
        {
            if (!declaredOutputDirectories.Contains(relative))
            {
                return false;
            }

            var path = PreparedInputFiles.SafePath(Root, relative);
            return Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any();
        }

        private void PrepareOutputDirectories()
        {
            if (!File.Exists(Path.Combine(Root, "tests", ModFolders.ManifestFile)))
            {
                return;
            }

            foreach (var test in TestManifest.LoadProject(Root).Integration)
            {
                foreach (var relative in ProjectAccess.OutputDirectories(test))
                {
                    Directory.CreateDirectory(PreparedInputFiles.SafePath(Root, relative));
                    var directory = relative;
                    while (!string.IsNullOrEmpty(directory))
                    {
                        declaredOutputDirectories.Add(directory);
                        directory = Path.GetDirectoryName(directory)?.Replace('\\', '/');
                    }
                }
            }
        }
    }
}
