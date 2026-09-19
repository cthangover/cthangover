namespace Cthangover.TestRunner
{
    /// <summary>
    /// Aggregated result of one suite entry (unit or a single integration
    /// scene) shown in the final summary and serialized to JSON. Check counts
    /// come from TRX for xUnit and from <c>[TEST_RESULT]</c> for scenes;
    /// <see cref="ExitCode"/>, <see cref="TimedOut"/> and <see cref="Note"/>
    /// keep the diagnostic context when a run fails.
    /// </summary>
    public class TestResult
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public bool Passed { get; set; }
        public bool TimedOut { get; set; }
        public int ExitCode { get; set; }
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public TimeSpan Duration { get; set; }
        public string Core { get; set; }
        public string Coverage { get; set; }
        public string Note { get; set; }
    }
}
