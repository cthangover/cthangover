namespace Cthangover.Core.Mods.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Immutable process-launch identity and output layout supplied before the child starts. Component user data
    /// may serve a sequential dependency chain; logs, reports, sandboxes and temporary files belong to this launch.
    /// Parsing and native-path validation never create files or call Godot.
    /// </summary>
    public sealed class TestExecutionContext
    {
        private static readonly string[] ContextKeys =
        {
            "RUN_ID", "ID", "ATTEMPT_ID", "LAUNCH_ID", "WORKER_ID", "PREPARED_REVISION",
            "INPUT_IDENTITY", "LEASE_ID", "ACCESS", "ARTIFACTS", "LOGS"
        };

        /// <summary>Parent invocation identity, independent of a reused prepared-input revision.</summary>
        public string RunId { get; }

        /// <summary>Catalogue selector attributed to this child process.</summary>
        public string TestId { get; }

        /// <summary>Attempt within the invocation; uncertain outcomes never reuse an output owner.</summary>
        public string AttemptId { get; }

        /// <summary>Unique preallocated process token; actual OS PID is published separately after startup.</summary>
        public string LaunchId { get; }

        /// <summary>Parent worker or lane identity for scheduling evidence.</summary>
        public string WorkerId { get; }

        /// <summary>Parent's content-fenced preparation revision, not a repository HEAD assumption.</summary>
        public string PreparedRevision { get; }

        /// <summary>Parent's checked input/build baseline identity.</summary>
        public string InputIdentity { get; }

        /// <summary>Parent input/data lease attribution; Core does not acquire or infer that lease.</summary>
        public string LeaseId { get; }

        /// <summary>Declared project access: read, output or write.</summary>
        public string Access { get; }

        /// <summary>Exclusive per-launch output root outside prepared inputs and dependency data.</summary>
        public string ArtifactRoot { get; }

        /// <summary>Private log subtree inside ArtifactRoot, shared only by this child's distinct sinks.</summary>
        public string LogRoot { get; }

        /// <summary>Expected prepared input or independently writable lane used as res://.</summary>
        public string ProjectRoot { get; }

        /// <summary>Expected native user:// root; only an ordered dependency component may share it.</summary>
        public string UserDataRoot { get; }

        /// <summary>Shared content-keyed compilation cache, protected by its existing owners and leases.</summary>
        public string CacheRoot { get; }

        /// <summary>Expected private process temp root, configured before Process.Start.</summary>
        public string TemporaryRoot { get; }

        /// <summary>Main game log; the native engine never owns this file.</summary>
        public string MainLogPath => Path.Combine(LogRoot, "cthangover.log");

        /// <summary>Separate game error sink with the same per-launch ownership as the main log.</summary>
        public string ErrorLogPath => Path.Combine(LogRoot, "cthangover_errors.log");

        /// <summary>Native engine sink passed by the parent before the user-argument separator.</summary>
        public string NativeLogPath => Path.Combine(LogRoot, "godot.log");

        /// <summary>Mod-test JSON/history root; sequential dependency launches do not overwrite one another's reports.</summary>
        public string ReportDirectory => Path.Combine(ArtifactRoot, "mod-tests");

        /// <summary>Default external capture directory for context-aware fixtures and parent --shots routing.</summary>
        public string ScreenshotsDirectory => Path.Combine(ArtifactRoot, "screens");

        /// <summary>Per-launch scratch root; existing mod/case sandbox subdivision stays with the test runner.</summary>
        public string SandboxDirectory => Path.Combine(ArtifactRoot, "sandbox");

        internal string LogIdentity => "run=" + RunId + " test=" + TestId + " attempt=" + AttemptId
            + " launch=" + LaunchId + " worker=" + WorkerId + " pid=" + System.Environment.ProcessId;

        private TestExecutionContext(IReadOnlyDictionary<string, string> environment)
        {
            RunId = Required(environment, "RUN_ID");
            TestId = Required(environment, "ID");
            AttemptId = Required(environment, "ATTEMPT_ID");
            LaunchId = Required(environment, "LAUNCH_ID");
            WorkerId = Required(environment, "WORKER_ID");
            PreparedRevision = Required(environment, "PREPARED_REVISION");
            InputIdentity = Required(environment, "INPUT_IDENTITY");
            LeaseId = Required(environment, "LEASE_ID");
            Access = Required(environment, "ACCESS");
            foreach (var character in LaunchId)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')
                {
                    throw new ArgumentException("Test launch identity must be a safe filename token");
                }
            }

            if (LaunchId is "." or ".." || Access is not "read" and not "output" and not "write")
            {
                throw new ArgumentException("Invalid test launch identity or project access");
            }

            ArtifactRoot = Absolute(Required(environment, "ARTIFACTS"));
            LogRoot = Absolute(Required(environment, "LOGS"));
            ProjectRoot = Absolute(Required(environment, "PROJECT"));
            UserDataRoot = Absolute(Required(environment, "DATA"));
            CacheRoot = Absolute(Required(environment, "CACHE"));
            if (!environment.TryGetValue("TEMP", out var temporary))
            {
                throw new ArgumentException("Test launch TEMP is missing");
            }

            TemporaryRoot = Absolute(temporary);
            foreach (var alias in new[] { "TMP", "TEMP", "TMPDIR" })
            {
                if (environment.TryGetValue(alias, out var path) && !SamePath(TemporaryRoot, path))
                {
                    throw new ArgumentException("Test launch temporary directories disagree");
                }
            }

            if (SamePath(ArtifactRoot, Path.GetPathRoot(ArtifactRoot)) || !Within(ArtifactRoot, LogRoot)
                || Overlap(ArtifactRoot, ProjectRoot) || Overlap(ArtifactRoot, UserDataRoot)
                || Overlap(ArtifactRoot, CacheRoot) || Overlap(TemporaryRoot, ProjectRoot)
                || Overlap(TemporaryRoot, UserDataRoot) || Overlap(TemporaryRoot, CacheRoot))
            {
                throw new ArgumentException("Test output roots overlap shared inputs/data or escape the launch");
            }

            CheckAncestors(ArtifactRoot);
            CheckAncestors(LogRoot);
            CheckAncestors(ReportDirectory);
            CheckAncestors(ScreenshotsDirectory);
            CheckAncestors(SandboxDirectory);
            CheckAncestors(TemporaryRoot);
        }

        /// <summary>
        /// Returns null for ordinary or legacy Verify-only launches. A partial new context, conflicting/duplicate
        /// user mirror, malformed identity or unsafe output layout refuses before any directory or log is created.
        /// </summary>
        public static TestExecutionContext Parse(IReadOnlyDictionary<string, string> environment,
            IReadOnlyList<string> userArguments)
        {
            ArgumentNullException.ThrowIfNull(environment);
            ArgumentNullException.ThrowIfNull(userArguments);
            var requested = false;
            foreach (var key in ContextKeys)
            {
                requested |= environment.ContainsKey("CTHANGOVER_TEST_" + key);
            }

            foreach (var argument in userArguments)
            {
                requested |= argument?.StartsWith("--test-", StringComparison.Ordinal) == true
                    && MirrorName(argument.Split('=')[0]) != null;
            }

            if (!requested)
            {
                return null;
            }

            var context = new TestExecutionContext(environment);
            context.ValidateMirrors(userArguments);
            return context;
        }

        /// <summary>
        /// Verifies actual bootstrap paths and the native log sink, returning its evidence source. A consumed
        /// native argument requires the parent's matching pre-start receipt; no output or native calls occur here.
        /// </summary>
        public string ValidateRuntimePaths(string actualProjectRoot, string actualUserDataRoot, string actualTemporaryRoot,
            IReadOnlyList<string> engineArguments, string parentLaunchReceipt = null)
        {
            Check(ProjectRoot, actualProjectRoot, "project");
            Check(UserDataRoot, actualUserDataRoot, "user data");
            Check(TemporaryRoot, actualTemporaryRoot, "temporary directory");
            var native = NativeLogArgument(engineArguments);
            if (native != null)
            {
                Check(NativeLogPath, native, "native log");
                return "engine-argument";
            }

            try
            {
                using var receipt = JsonDocument.Parse(parentLaunchReceipt ?? "null");
                var root = receipt.RootElement;
                if (root.ValueKind != JsonValueKind.Object || root.GetProperty("schemaVersion").GetInt32() != 1
                    || root.GetProperty("runId").GetString() != RunId || root.GetProperty("testId").GetString() != TestId
                    || root.GetProperty("attemptId").GetString() != AttemptId
                    || root.GetProperty("launchId").GetString() != LaunchId)
                {
                    throw new ArgumentException("Test native launch receipt identity mismatch");
                }

                Check(NativeLogPath, root.GetProperty("nativeLogPath").GetString(), "native receipt log");
                var arguments = new List<string>();
                foreach (var item in root.GetProperty("engineArguments").EnumerateArray())
                {
                    arguments.Add(item.GetString());
                }

                var argumentPath = NativeLogArgument(arguments);
                if (argumentPath == null)
                {
                    throw new ArgumentException("Test native launch receipt has no engine log argument");
                }

                Check(NativeLogPath, argumentPath, "native receipt argument");
                return "parent-launch-receipt";
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new ArgumentException("Test native launch receipt is missing or malformed", error);
            }
        }

        private static string NativeLogArgument(IReadOnlyList<string> arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            string path = null;
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i] ?? throw new ArgumentException("Null engine argument");
                if (argument is "--" or "++")
                {
                    break;
                }

                string candidate = null;
                if (argument.StartsWith("--log-file=", StringComparison.Ordinal))
                {
                    candidate = argument["--log-file=".Length..];
                }
                else if (argument == "--log-file")
                {
                    if (++i >= arguments.Count)
                    {
                        throw new ArgumentException("Test native log argument has no value");
                    }

                    candidate = arguments[i];
                }

                if (candidate != null)
                {
                    if (path != null)
                    {
                        throw new ArgumentException("Duplicate test native log argument");
                    }

                    path = Absolute(candidate);
                }
            }

            return path;
        }

        private void ValidateMirrors(IReadOnlyList<string> arguments)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i] ?? throw new ArgumentException("Null user argument");
                var separator = argument.IndexOf('=');
                var flag = separator < 0 ? argument : argument[..separator];
                var name = MirrorName(flag);
                if (name == null && flag != "--log-file")
                {
                    continue;
                }

                if (!seen.Add(flag))
                {
                    throw new ArgumentException("Duplicate test context argument: " + flag);
                }

                string value;
                if (separator >= 0)
                {
                    value = argument[(separator + 1)..];
                }
                else
                {
                    if (++i >= arguments.Count)
                    {
                        throw new ArgumentException("Missing test context argument value: " + flag);
                    }

                    value = arguments[i];
                }

                var expected = name switch
                {
                    "RUN_ID" => RunId,
                    "TEST_ID" => TestId,
                    "ATTEMPT_ID" => AttemptId,
                    "LAUNCH_ID" => LaunchId,
                    "WORKER_ID" => WorkerId,
                    "ARTIFACTS" => ArtifactRoot,
                    "LOGS" => LogRoot,
                    _ => MainLogPath
                };
                var matches = name is "ARTIFACTS" or "LOGS" || name == null
                    ? SamePath(expected, value) : expected == value;
                if (!matches)
                {
                    throw new ArgumentException("Test context argument disagrees with environment: " + flag);
                }
            }
        }

        private static string MirrorName(string flag)
        {
            return flag switch
            {
                "--test-run-id" => "RUN_ID",
                "--test-test-id" => "TEST_ID",
                "--test-attempt-id" => "ATTEMPT_ID",
                "--test-launch-id" => "LAUNCH_ID",
                "--test-worker-id" => "WORKER_ID",
                "--test-output-dir" => "ARTIFACTS",
                "--test-log-dir" => "LOGS",
                _ => null
            };
        }

        private static string Required(IReadOnlyDictionary<string, string> environment, string suffix)
        {
            var key = "CTHANGOVER_TEST_" + suffix;
            if (!environment.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value) || value.Length > 1024)
            {
                throw new ArgumentException("Missing or invalid test context value: " + key);
            }

            foreach (var character in value)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException("Control character in test context value: " + key);
                }
            }

            return value;
        }

        internal static string Absolute(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException("Test context paths must be absolute");
            }

            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        internal static bool SamePath(string first, string second)
        {
            return string.Equals(Absolute(first), Absolute(second), OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        internal static void Check(string expected, string actual, string name)
        {
            if (!SamePath(expected, actual))
            {
                throw new ArgumentException($"Test {name} isolation mismatch: expected '{expected}', actual '{actual}'");
            }
        }

        private static bool Within(string parent, string child)
        {
            var relative = Path.GetRelativePath(parent, child);
            return relative != "." && relative != ".." && !Path.IsPathRooted(relative)
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static bool Overlap(string first, string second)
        {
            return SamePath(first, second) || Within(first, second) || Within(second, first);
        }

        private static void CheckAncestors(string path)
        {
            for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
            {
                if ((current.Exists || File.Exists(current.FullName))
                    && (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new ArgumentException("Test output path contains a symbolic link or reparse point");
                }
            }
        }
    }
}
