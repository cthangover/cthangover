namespace Cthangover.TestRunner
{
    /// <summary>Caller-owned synchronous progress boundary for diagnostics and deterministic cancellation.</summary>
    public interface IInputRetentionObserver
    {
        /// <summary>Observes retiring, file-deleted, directory-deleted, deferred or completed before the next work unit.</summary>
        void Observe(string phase, string store, InputRetentionDecision decision);
    }
}
