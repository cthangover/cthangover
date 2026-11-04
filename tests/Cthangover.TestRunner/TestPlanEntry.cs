namespace Cthangover.TestRunner
{
    /// <summary>An immutable selection decision enriched with prerequisite and environment ownership.</summary>
    public class TestPlanEntry
    {
        public IntegrationTestDefinition Test { get; init; }
        public string Reason { get; set; }
        public string Environment { get; set; }
        public string Filter { get; set; }
        public string ModFilter { get; set; }
    }
}
