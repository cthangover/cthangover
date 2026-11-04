namespace Cthangover.TestRunner
{
    /// <summary>The terminal marker a particular launch must produce; aliases and battle variants are explicit.</summary>
    public class ResultContract
    {
        public string Name { get; set; }
        public string Core { get; set; }
        public string ChildPrefix { get; set; }
        public bool AllowSkipped { get; set; } = true;
        public List<DiagnosticExpectation> ExpectedErrors { get; set; } = new();
    }
}
