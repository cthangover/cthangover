namespace Cthangover.TestRunner
{
    /// <summary>
    /// Machine-readable outcome extracted from a scene's output: the uniform
    /// <c>[TEST_RESULT]</c> line (with optional battle <c>core=</c> detail)
    /// and, for the battle coverage scene, the <c>coverage: N/M</c> summary.
    /// </summary>
    public class ParsedSceneResult
    {
        public bool HasResult { get; set; }
        public string Name { get; set; }
        public string Core { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public List<string> Problems { get; set; } = new();
        public string Coverage { get; set; }
    }
}
