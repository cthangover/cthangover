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
        /// <summary>Stable catalogue and dependency identity, independent of a particular process launch token.</summary>
        public string Name
        {
            get; set;
        }
        /// <summary>Exact-case project-relative native scene path checked before process creation.</summary>
        public string Scene
        {
            get; set;
        }
        /// <summary>
        /// User arguments and declared output scopes; launch routing redirects outputs without changing gameplay options.
        /// </summary>
        public string[] Args
        {
            get; set;
        }

        /// <summary>Execution adapter: integration, mod-unit, unit, or manual (catalogue only).</summary>
        public string Kind { get; set; } = "integration";
        /// <summary>Module identities used for selection and suite-registration coverage.</summary>
        public string[] Owners { get; set; } = Array.Empty<string>();
        /// <summary>Named selection memberships; absence of explicit selection uses the existing full profile.</summary>
        public string[] Profiles { get; set; } = Array.Empty<string>();
        /// <summary>Orthogonal catalogue labels, including negative or individual-case admission hints.</summary>
        public string[] Tags { get; set; } = Array.Empty<string>();
        /// <summary>Prerequisites sharing ordered component state; unsuccessful prerequisites block descendants.</summary>
        public string[] DependsOn { get; set; } = Array.Empty<string>();
        /// <summary>Optional authored collection identity used by catalogue filters.</summary>
        public string Game
        {
            get; set;
        }
        /// <summary>Optional authored renderer-variant label, not an implicit engine override.</summary>
        public string Renderer
        {
            get; set;
        }
        /// <summary>Manifest or suite path retained as catalogue provenance.</summary>
        public string Source
        {
            get; set;
        }
        /// <summary>Explains why an inert catalogue entry requires an external workflow.</summary>
        public string ManualReason
        {
            get; set;
        }
        /// <summary>External-handshake launches require their exact --test name; profiles, globs and defaults cannot admit them.</summary>
        public bool RequiresExplicitSelection
        {
            get; set;
        }
        /// <summary>Concrete manual prerequisites exported with the catalogue.</summary>
        public string[] Requirements { get; set; } = Array.Empty<string>();
        /// <summary>Explicit terminal/error contract; a process exit or readiness marker alone never passes it.</summary>
        public ResultContract Result
        {
            get; set;
        }

        /// <summary>Project access contract: read, declared output directories only, or an exclusive writable copy.</summary>
        public string Project { get; set; } = "write";

        /// <summary>Measured child duration used only for scheduling; absent hints use a quarter of the timeout.</summary>
        public double? Seconds
        {
            get; set;
        }

        /// <summary>
        /// When true the scene runs with a real window instead of
        /// <c>--headless</c>. Headless uses a dummy rendering server, so
        /// viewport captures are empty — screenshot/CV tests need this flag.
        /// </summary>
        public bool Windowed
        {
            get; set;
        }

        /// <summary>Parent-supervised process lifetime, including driver execution and bounded termination.</summary>
        public int TimeoutSeconds { get; set; } = 120;
    }
}
