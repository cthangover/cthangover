namespace Cthangover.TestRunner
{
    using System.Text.Json;
    using System.Text.RegularExpressions;
    /// <summary>
    /// Deserialized <c>tests/tests.manifest.json</c> (with the parts of the mods
    /// folders, see <see cref="LoadProject"/>) — the single source of
    /// truth for which tests the global runner executes and in what order.
    /// Previously the same list was duplicated across PowerShell scripts and
    /// the coverage map, which drifted; now scripts and code read this file.
    /// </summary>
    public class TestManifest
    {
        public UnitManifest Unit { get; set; } = new();
        public List<IntegrationTestDefinition> Integration { get; set; } = new();
        public int SchemaVersion { get; set; }
        public Dictionary<string, ManualTestPolicy> ManualSuites { get; set; } = new();
        public Dictionary<string, string> ManualScenes { get; set; } = new();

        public static TestManifest Load(string path)
        {
            var manifest = Read(path);
            manifest.Validate();
            return manifest;
        }

        /// <summary>
        /// The manifest of a whole project: <c>tests/tests.manifest.json</c> plus the part every
        /// mods folder may keep at <c>&lt;folder&gt;/tests.manifest.json</c>
        /// (<see cref="ModFolders"/>). A part contributes only <c>integration</c> entries and
        /// <c>manualSuites</c> policies, appended after the project's own in folder order; its
        /// other sections are ignored, so it needs no <c>unit</c> block. The merged result is
        /// validated as one manifest: names stay unique across the parts and a part may depend
        /// on tests of the project manifest.
        /// </summary>
        public static TestManifest LoadProject(string root)
        {
            var manifest = Read(Path.Combine(root, "tests", ModFolders.ManifestFile));
            foreach (var folder in ModFolders.Names)
            {
                var path = Path.Combine(root, folder, ModFolders.ManifestFile);
                if (!File.Exists(path)) continue;
                var part = Read(path);
                manifest.Integration.AddRange(part.Integration);
                foreach (var policy in part.ManualSuites)
                    if (!manifest.ManualSuites.TryAdd(policy.Key, policy.Value))
                        throw new InvalidOperationException($"duplicate manual suite policy '{policy.Key}' in {path}");
            }
            manifest.Validate();
            return manifest;
        }

        private static TestManifest Read(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"test manifest not found: {path}");
            var json = File.ReadAllText(path);
            var manifest = JsonSerializer.Deserialize<TestManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (manifest == null)
                throw new InvalidOperationException($"test manifest is empty or invalid: {path}");
            return manifest;
        }

        /// <summary>Rejects ambiguous contracts and dependency graphs before a process or workspace is created.</summary>
        public void Validate()
        {
            if (SchemaVersion != 2) throw new InvalidOperationException("test manifest schemaVersion must be 2");
            if (string.IsNullOrWhiteSpace(Unit?.Project) || Unit.TimeoutSeconds <= 0)
                throw new InvalidOperationException("unit project and positive timeout are required");
            if (Unit.Seconds.HasValue && (!double.IsFinite(Unit.Seconds.Value) || Unit.Seconds <= 0))
                throw new InvalidOperationException("unit seconds must be positive and finite");
            if (Path.IsPathRooted(Unit.Project) || Unit.Project.Replace('\\', '/').Split('/').Contains(".."))
                throw new InvalidOperationException("unit project must stay inside the project workspace");
            var names = new HashSet<string>(StringComparer.Ordinal) { "unit" };
            foreach (var test in Integration)
            {
                if (string.IsNullOrWhiteSpace(test.Name) || !Regex.IsMatch(test.Name, @"\A[a-zA-Z0-9_.:-]+\z") || !names.Add(test.Name))
                    throw new InvalidOperationException($"invalid or duplicate test name '{test.Name}'");
                if (test.Result == null || string.IsNullOrWhiteSpace(test.Result.Name))
                    throw new InvalidOperationException($"{test.Name}: explicit result contract is required");
                if (test.Result.ExpectedErrors == null || test.Result.ExpectedErrors.Any(error => string.IsNullOrWhiteSpace(error.Pattern) || error.Count <= 0))
                    throw new InvalidOperationException($"{test.Name}: expected engine errors require a specific pattern and positive count");
                foreach (var error in test.Result.ExpectedErrors) _ = new Regex(error.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
                if (test.TimeoutSeconds <= 0 || test.TimeoutSeconds > 86400 || string.IsNullOrWhiteSpace(test.Scene))
                    throw new InvalidOperationException($"{test.Name}: scene and valid timeout are required");
                if (test.Kind is not ("integration" or "mod-unit")) throw new InvalidOperationException($"{test.Name}: unknown execution kind '{test.Kind}'");
                if (test.Owners == null || test.Owners.Length == 0 || test.Profiles == null || test.Profiles.Length == 0)
                    throw new InvalidOperationException($"{test.Name}: owners and profiles are required");
                if (test.DependsOn == null || test.Tags == null || test.Args == null)
                    throw new InvalidOperationException($"{test.Name}: args, tags and dependsOn cannot be null");
                ProjectAccess.Validate(test);
                if (test.Owners.Concat(test.Profiles).Concat(test.DependsOn).Concat(test.Tags).Concat(test.Args).Any(string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException($"{test.Name}: metadata contains an empty value");
            }
            var definitions = Integration.ToDictionary(t => t.Name, StringComparer.Ordinal);
            definitions.Add("unit", new IntegrationTestDefinition { Name = "unit" });
            foreach (var test in Integration)
                foreach (var dependency in test.DependsOn)
                    if (!definitions.ContainsKey(dependency))
                        throw new InvalidOperationException($"{test.Name}: unknown dependency '{dependency}'");
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var test in Integration) Visit(test, definitions, done, new HashSet<string>(StringComparer.Ordinal));
            ProjectAccess.ValidateOutputs(Integration);
        }

        private static void Visit(IntegrationTestDefinition test, Dictionary<string, IntegrationTestDefinition> definitions,
            HashSet<string> done, HashSet<string> active)
        {
            if (done.Contains(test.Name)) return;
            if (!active.Add(test.Name)) throw new InvalidOperationException("test dependency cycle at " + test.Name);
            foreach (var name in test.DependsOn) Visit(definitions[name], definitions, done, active);
            active.Remove(test.Name);
            done.Add(test.Name);
        }
    }
}
