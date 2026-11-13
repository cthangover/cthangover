namespace Cthangover.TestRunner
{
    /// <summary>A dependency component holds its exclusive lane across all launches, including blocked descendants.</summary>
    internal sealed class ScheduledComponent
    {
        internal List<TestPlanEntry> Entries { get; }
        internal bool Writable { get; }
        internal double Seconds { get; }
        internal int Next { get; set; }
        internal int? Lane { get; set; }
        internal Task<TestResult> Running { get; set; }
        internal TestPlanEntry Entry => Entries[Next];
        internal bool Done => Next == Entries.Count;

        internal ScheduledComponent(IEnumerable<TestPlanEntry> entries)
        {
            Entries = entries.ToList();
            Writable = Entries.Any(entry => entry.Test.Project == "write");
            Seconds = Entries.Sum(entry => entry.Test.Seconds ?? entry.Test.TimeoutSeconds / 4.0);
        }
    }
}
