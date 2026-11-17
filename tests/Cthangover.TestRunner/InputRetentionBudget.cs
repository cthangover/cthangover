namespace Cthangover.TestRunner
{
    using System.Diagnostics;

    /// <summary>One call-wide cooperative budget; progress never hides cancellation or uninterruptible filesystem time.</summary>
    internal sealed class InputRetentionBudget
    {
        private readonly int maximum;
        private readonly double seconds;
        private readonly CancellationToken cancellation;
        private readonly IInputRetentionObserver observer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long used;
        private long pending;
        private long pendingAttributes;
        private long pendingEntries;

        internal InputRetentionBudget(RetentionWorkOptions options, CancellationToken cancellation,
            IInputRetentionObserver observer)
        {
            options ??= new RetentionWorkOptions();
            if (options.MaxWorkUnits < 0 || !double.IsFinite(options.MaxSeconds) || options.MaxSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }
            maximum = options.MaxWorkUnits;
            seconds = options.MaxSeconds;
            this.cancellation = cancellation;
            this.observer = observer;
            cancellation.ThrowIfCancellationRequested();
        }

        internal string Reason => used >= maximum ? "work-budget" : "time-budget";

        internal double ElapsedSeconds => clock.Elapsed.TotalSeconds;

        internal void Attach(InputRetentionDecision decision)
        {
            decision.WorkUnits += pending;
            decision.AttributeReads += pendingAttributes;
            decision.EntriesVisited += pendingEntries;
            pending = 0;
            pendingAttributes = 0;
            pendingEntries = 0;
        }

        internal void AttributeRead()
        {
            pendingAttributes++;
        }

        internal void EntryVisited()
        {
            pendingEntries++;
        }

        internal bool GuardPath(string path)
        {
            var current = Path.GetFullPath(path);
            while (current != null)
            {
                if (!Take(null))
                {
                    return false;
                }
                AttributeRead();
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Linked retention path refused: " + current);
                }
                current = Path.GetDirectoryName(current);
            }
            return true;
        }

        internal bool Take(InputRetentionDecision decision)
        {
            cancellation.ThrowIfCancellationRequested();
            if (used >= maximum || clock.Elapsed.TotalSeconds >= seconds)
            {
                return false;
            }
            used++;
            if (decision == null)
            {
                pending++;
            }
            else
            {
                decision.WorkUnits++;
            }
            return true;
        }

        internal void Observe(string phase, string store, InputRetentionDecision decision)
        {
            observer?.Observe(phase, store, decision);
            cancellation.ThrowIfCancellationRequested();
        }
    }
}
