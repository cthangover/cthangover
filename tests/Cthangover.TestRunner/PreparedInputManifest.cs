namespace Cthangover.TestRunner
{
    /// <summary>Atomic ready receipt; failed preparation cannot publish a usable generation.</summary>
    public sealed class PreparedInputManifest
    {
        /// <summary>Version of selection, fixed override and publication policy.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Canonical source identity, retained only as provenance and never used for delta restoration.</summary>
        public string Source
        {
            get; set;
        }

        /// <summary>Actual ordered content/policy digest captured before and after preparation.</summary>
        public string SourceFingerprint
        {
            get; set;
        }

        /// <summary>Checked immutable source files after the single fixed user-dir override is applied.</summary>
        public List<PreparedInputFile> Files { get; set; } = new();

        /// <summary>Build receipt for these effective inputs; null only for pure fixture consumers.</summary>
        public PreparedBuildReceipt Build
        {
            get; set;
        }
    }
}
