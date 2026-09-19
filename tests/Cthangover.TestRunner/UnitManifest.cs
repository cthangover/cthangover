namespace Cthangover.TestRunner
{
    /// <summary>
    /// Unit-test section of <c>tests/tests.manifest.json</c>: the xUnit
    /// project to run via <c>dotnet test</c> and its timeout.
    /// </summary>
    public class UnitManifest
    {
        public string Project { get; set; }
        public int TimeoutSeconds { get; set; } = 300;
    }
}
