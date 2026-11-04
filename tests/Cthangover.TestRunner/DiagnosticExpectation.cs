namespace Cthangover.TestRunner
{
    /// <summary>A deliberately provoked engine error must have a specific pattern and exact count.</summary>
    public class DiagnosticExpectation
    {
        public string Pattern { get; set; }
        public int Count { get; set; } = 1;
    }
}
