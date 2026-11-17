namespace Cthangover.TestRunner
{
    /// <summary>Successful build identity and actual output hashes, separate from runtime asset revision.</summary>
    public sealed class PreparedBuildReceipt
    {
        /// <summary>Effective compiler input/toolchain digest.</summary>
        public string Identity
        {
            get; set;
        }

        /// <summary>Whether this receipt includes the unit-test executable assembly.</summary>
        public bool IncludesUnit
        {
            get; set;
        }

        /// <summary>Complete output file evidence checked before reuse and no-build admission.</summary>
        public List<PreparedInputFile> Outputs { get; set; } = new();
    }
}
