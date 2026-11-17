namespace Cthangover.TestRunner
{
    /// <summary>Caps cooperative cleanup work; a synchronous OS call itself cannot be given a hard wall deadline.</summary>
    public sealed class RetentionWorkOptions
    {
        /// <summary>Maximum explicit enumeration, metadata or mutation units across one complete Prune call.</summary>
        public int MaxWorkUnits { get; set; } = 100000;

        /// <summary>Elapsed boundary checked between operations, independently of an external process watchdog.</summary>
        public double MaxSeconds { get; set; } = 10;
    }
}
