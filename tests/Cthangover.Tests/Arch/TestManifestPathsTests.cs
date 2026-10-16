using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Engine.Tests.Arch
{
    /// <summary>
    /// Every scene in <c>tests/tests.manifest.json</c> must exist with exactly
    /// the spelled casing. Windows resolves a wrong-case <c>res://</c> path,
    /// Linux/CI and exported packs do not — such an entry silently fails there
    /// with "no result marker". This keeps the manifest portable.
    /// </summary>
    public class TestManifestPathsTests
    {
        [Fact]
        public void ManifestScenes_ExistWithExactCase()
        {
            var root = RepoPaths.FindRepoRoot();
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests", "tests.manifest.json")));

            var problems = manifest.RootElement.GetProperty("integration").EnumerateArray()
                .Select(t => (Name: t.GetProperty("name").GetString(), Scene: t.GetProperty("scene").GetString()))
                .Where(t => t.Scene.StartsWith("res://", StringComparison.Ordinal))
                .Where(t => !RepoPaths.ExistsWithExactCase(root, t.Scene.Substring("res://".Length)))
                .Select(t => $"{t.Name}: {t.Scene}")
                .ToList();

            Assert.True(problems.Count == 0, "missing or wrong-case scenes:\n" + string.Join("\n", problems));
        }

        [Fact]
        public void ProjectAssemblyName_MatchesCoreAssembly()
        {
            // Godot loads <assembly_name>.dll: a case mismatch with the
            // csproj output works on Windows only.
            var root = RepoPaths.FindRepoRoot();
            var expected = typeof(Cthangover.Core.Settings.GameData).Assembly.GetName().Name;
            var lines = File.ReadAllLines(Path.Combine(root, "project.godot"))
                .Where(l => l.StartsWith("project_assembly_name=", StringComparison.Ordinal)
                    || l.StartsWith("project/assembly_name=", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(lines);
            foreach (var line in lines)
                Assert.Equal($"\"{expected}\"", line.Substring(line.IndexOf('=') + 1));
        }
    }
}
