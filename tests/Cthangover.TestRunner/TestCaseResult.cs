namespace Cthangover.TestRunner
{
    /// <summary>A leaf result retained from TRX or a mod-test report, including skip reasons and original stacks.</summary>
    public class TestCaseResult
    {
        public string Name { get; set; }
        public string Mod { get; set; }
        public string Status { get; set; }
        public double DurationSeconds { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public List<string> Artifacts { get; set; } = new();
        public List<string> Log { get; set; } = new();
    }
}
