using System.Collections.Generic;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Thread-safe accumulator of every test outcome of a run (plan §7.1).
    /// Tests write nothing themselves: the runner records each outcome here
    /// and <see cref="ModTestReportWriter"/> flushes the ledger to the output
    /// channels. Suite summaries are flushed after each mod (§7.4), so a
    /// later mod crashing the process does not lose earlier statistics.
    /// </summary>
    public sealed class ModTestLedger
    {
        private readonly object gate = new();
        private readonly List<ModTestEntry> entries = new();
        private readonly List<ModTestSuiteSummary> suites = new();

        /// <summary>Appends one test outcome.</summary>
        public void Add(ModTestEntry entry)
        {
            lock (gate)
                entries.Add(entry);
        }

        /// <summary>Appends a completed per-mod summary (snapshot taken by the runner).</summary>
        public void AddSuite(ModTestSuiteSummary summary)
        {
            lock (gate)
                suites.Add(summary);
        }

        /// <summary>Snapshot of every recorded test outcome in arrival order.</summary>
        public List<ModTestEntry> SnapshotEntries()
        {
            lock (gate)
                return new List<ModTestEntry>(entries);
        }

        /// <summary>Snapshot of every completed per-mod summary.</summary>
        public List<ModTestSuiteSummary> SnapshotSuites()
        {
            lock (gate)
                return new List<ModTestSuiteSummary>(suites);
        }

        /// <summary>Removes all recorded data (used between engine runs).</summary>
        public void Clear()
        {
            lock (gate)
            {
                entries.Clear();
                suites.Clear();
            }
        }
    }
}
