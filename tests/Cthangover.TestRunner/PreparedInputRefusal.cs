namespace Cthangover.TestRunner
{
    /// <summary>A failed preparation remains explicit in reports and cannot trigger an unreported full-copy fallback.</summary>
    public sealed class PreparedInputRefusal : IOException
    {
        /// <summary>Retains the exact reason and measured work completed before refusal.</summary>
        public PreparedInputRefusal(string reason, InputPreparationMetrics metrics, Exception inner = null)
            : base(reason, inner)
        {
            Reason = reason;
            Metrics = metrics;
            if (!metrics.ConflictReasons.Contains(reason))
            {
                metrics.ConflictReasons.Add(reason);
            }
        }

        /// <summary>Measured preparation state, including actual copies before failure and its conflict reasons.</summary>
        public InputPreparationMetrics Metrics
        {
            get;
        }

        /// <summary>Stable reason code for callers and refusal tests, independent of localized diagnostic text.</summary>
        public string Reason
        {
            get;
        }
    }
}
