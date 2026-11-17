namespace Cthangover.TestRunner
{
    /// <summary>Identity and ownership of one process launch; terminal PASS remains the existing process/result contract.</summary>
    public sealed class LaunchExecutionMetrics
    {
        /// <summary>Versioned additive launch metadata schema.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Unique runner invocation identity.</summary>
        public string RunId
        {
            get; set;
        }

        /// <summary>Original catalogue test name.</summary>
        public string TestId
        {
            get; set;
        }

        /// <summary>Attempt identity; new attempts never reuse a previous output root.</summary>
        public string AttemptId
        {
            get; set;
        }

        /// <summary>Unique token allocated before OS process creation.</summary>
        public string LaunchId
        {
            get; set;
        }

        /// <summary>Scheduler worker/lane name.</summary>
        public string WorkerId
        {
            get; set;
        }

        /// <summary>Verified baseline content generation.</summary>
        public string PreparedInputId
        {
            get; set;
        }

        /// <summary>Actual content/policy digest used for this child.</summary>
        public string SourceFingerprint
        {
            get; set;
        }

        /// <summary>Active input/lane lease protecting the project's lifetime.</summary>
        public string LeaseId
        {
            get; set;
        }

        /// <summary>Explicit execution preparation policy; no hidden fresh-copy fallback.</summary>
        public string WorkspaceMode
        {
            get; set;
        }

        /// <summary>Actual process res:// input/write root.</summary>
        public string ProjectRoot
        {
            get; set;
        }

        /// <summary>Private component save/user data; only an ordered dependency chain may share it.</summary>
        public string DataDirectory
        {
            get; set;
        }

        /// <summary>Uniquely claimed per-launch artifact root.</summary>
        public string OutputDirectory
        {
            get; set;
        }

        /// <summary>Unique launch temp, set before process bootstrap.</summary>
        public string TemporaryDirectory
        {
            get; set;
        }

        /// <summary>Exclusive main/error/native log root, distinct from component state.</summary>
        public string LogsDirectory
        {
            get; set;
        }

        /// <summary>Actual copies charged to this launch, excluding separately recorded baseline preparation.</summary>
        public InputTransferCount InputsCopied { get; set; } = new();

        /// <summary>Explicit path/access/lease refusal reasons.</summary>
        public List<string> ConflictReasons { get; set; } = new();
    }
}
