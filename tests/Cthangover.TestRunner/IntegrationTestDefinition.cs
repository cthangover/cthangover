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

        /// <summary>Execution adapter: integration, mod-unit, unit, or manual (catalogue only).</summary>
        public string Kind { get; set; } = "integration";
        public string[] Owners { get; set; } = Array.Empty<string>();
        public string[] Profiles { get; set; } = Array.Empty<string>();
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string[] DependsOn { get; set; } = Array.Empty<string>();
        public string Game { get; set; }
        public string Renderer { get; set; }
        public string Source { get; set; }
        public string ManualReason { get; set; }
        public string[] Requirements { get; set; } = Array.Empty<string>();
        public ResultContract Result { get; set; }

        /// <summary>Project access contract: read, declared output directories only, or an exclusive writable copy.</summary>
        public string Project { get; set; } = "write";

        /// <summary>Measured child duration used only for scheduling; absent hints use a quarter of the timeout.</summary>
        public double? Seconds { get; set; }

        /// <summary>
        /// When true the scene runs with a real window instead of
        /// <c>--headless</c>. Headless uses a dummy rendering server, so
        /// viewport captures are empty — screenshot/CV tests need this flag.
        /// </summary>
        public bool Windowed { get; set; }

        public int TimeoutSeconds { get; set; } = 120;
    }
}
