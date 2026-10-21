#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Godot;

    /// <summary>
    /// Test names of <c>tests/tests.manifest.json</c>. The coverage guards map
    /// subsystems and battle cores to the test that covers them; a mapping to a
    /// name the runner never runs is no coverage, so the guards check it here.
    /// </summary>
    public static class TestManifestNames
    {
        /// <summary>Every integration test name of the runner manifest; empty when it cannot be read.</summary>
        public static HashSet<string> Load()
        {
            var names = new HashSet<string>();
            var path = ProjectSettings.GlobalizePath("res://tests/tests.manifest.json");
            if (!File.Exists(path))
                return names;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("integration", out var tests) && tests.ValueKind == JsonValueKind.Array)
            {
                foreach (var test in tests.EnumerateArray())
                {
                    if (test.TryGetProperty("name", out var name))
                        names.Add(name.GetString());
                }
            }
            return names;
        }
    }
}
#endif
