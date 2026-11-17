namespace Cthangover.TestRunner
{
    /// <summary>Synchronous progress instrumentation; never retained after acquisition or substituted for real preparation.</summary>
    public interface IPreparedInputObserver
    {
        /// <summary>Receives scanned/materialized/before-seal while the candidate generation is still unpublished.</summary>
        void Observe(string phase, string root, string sourceFingerprint);
    }
}
