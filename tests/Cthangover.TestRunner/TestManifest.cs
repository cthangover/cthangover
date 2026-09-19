using System.Text.Json;

namespace Cthangover.TestRunner
{
    /// <summary>
    /// Deserialized <c>tests/tests.manifest.json</c> — the single source of
    /// truth for which tests the global runner executes and in what order.
    /// Previously the same list was duplicated across PowerShell scripts and
    /// the coverage map, which drifted; now scripts and code read this file.
    /// </summary>
    public class TestManifest
    {
        public UnitManifest Unit { get; set; } = new();
        public List<IntegrationTestDefinition> Integration { get; set; } = new();

        public static TestManifest Load(string path)
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
    }
}
