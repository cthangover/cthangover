namespace Cthangover.TestRunner
{
    /// <summary>Durable evidence accompanies an OS-held lease; PID plus process start prevents accidental stale-owner reuse.</summary>
    public sealed class PreparedInputOwner
    {
        /// <summary>Version of this lease evidence object.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Unique lease token used by run, workspace and child launch receipts.</summary>
        public string LeaseId
        {
            get; set;
        }

        /// <summary>Invocation identity when supplied; fixture acquisitions use their lease token.</summary>
        public string RunId
        {
            get; set;
        }

        /// <summary>Content generation protected by this lease.</summary>
        public string PreparedInputId
        {
            get; set;
        }

        /// <summary>Absolute owned prepared root, never the mutable original source.</summary>
        public string Root
        {
            get; set;
        }

        /// <summary>Read or write admission represented by the separate kernel file lease.</summary>
        public string Access
        {
            get; set;
        }

        /// <summary>Actual owner process identity; not inferred from a potentially reused filename.</summary>
        public int ProcessId
        {
            get; set;
        }

        /// <summary>UTC process-start ticks used together with PID.</summary>
        public long ProcessStartedTicks
        {
            get; set;
        }

        /// <summary>False after graceful disposal; crash recovery still checks OS lock and process identity.</summary>
        public bool Active
        {
            get; set;
        }

        /// <summary>Most recent durable ownership update.</summary>
        public DateTimeOffset UpdatedAt
        {
            get; set;
        }
    }
}
