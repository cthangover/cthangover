namespace Cthangover.TestRunner
{
    /// <summary>Validated TRX counters and leaf outcomes; an absent or incomplete TRX is an infrastructure failure.</summary>
    public class TrxReport
    {
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public List<TestCaseResult> Cases { get; set; } = new();
        public List<string> Problems { get; set; } = new();
    }
}
