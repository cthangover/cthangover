namespace Cthangover.TestRunner
{
    /// <summary>Actual successful input materializations attributed to one preparation or launch.</summary>
    public sealed class InputTransferCount
    {
        /// <summary>Number of files physically materialized; reuse and receipt writes do not increment it.</summary>
        public long Files
        {
            get; set;
        }

        /// <summary>Actual bytes materialized in those files, independent of OS/device write amplification.</summary>
        public long Bytes
        {
            get; set;
        }
    }
}
