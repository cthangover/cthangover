namespace Cthangover.TestRunner
{
    /// <summary>
    /// One integration scene from <c>tests/tests.manifest.json</c>. The runner
    /// passes <see cref="Scene"/> plus <see cref="Args"/> to Godot, parses the
    /// scene's <c>[TEST_RESULT]</c> line and enforces <see cref="TimeoutSeconds"/>.
    /// Adding a test is a manifest edit only — no runner code changes.
    /// </summary>
    public class IntegrationTestDefinition
    {
        public string Name { get; set; }
        public string Scene { get; set; }
        public string[] Args { get; set; }
        public int TimeoutSeconds { get; set; } = 120;
    }
}
