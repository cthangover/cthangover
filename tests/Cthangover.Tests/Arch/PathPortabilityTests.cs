using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Engine.Tests.Arch
{
    /// <summary>
    /// Project assets must stay portable: no machine-specific absolute paths
    /// and no references whose casing differs from the file on disk. Windows
    /// forgives both, Linux/CI and exported packs do not — so these checks run
    /// in xUnit on every platform and fail on the author's machine first.
    /// <c>res://</c> references inside a mod resolve against the project root
    /// or the mod root, matching the TSCN resolver.
    /// </summary>
    public class PathPortabilityTests
    {
        private static readonly string[] AssetExtensions =
        {
            ".tscn", ".tres", ".json", ".scenario", ".gd", ".gdshader", ".godot", ".cfg", ".csproj", ".sln",
        };

        private static readonly Regex AbsolutePath = new(
            @"(?<![A-Za-z0-9_])[A-Za-z]:(\\\\|\\|/)[A-Za-z]|(?<![\w.\-/])(/home/[a-z]|/Users/[A-Za-z]|/workspace/|/mnt/[a-z]/)",
            RegexOptions.Compiled);

        private static readonly Regex ResReference = new(@"res://([^""'\s)\]>,;]+)", RegexOptions.Compiled);

        [Fact]
        public void ProjectAssets_HaveNoAbsolutePaths()
        {
            var root = RepoPaths.FindRepoRoot();
            var problems = new List<string>();

            foreach (var file in AssetFiles(root))
            {
                var lines = File.ReadAllLines(Path.Combine(root, file));
                for (var i = 0; i < lines.Length; i++)
                {
                    if (AbsolutePath.IsMatch(lines[i]))
                        problems.Add($"{file}:{i + 1}: {lines[i].Trim()}");
                }
            }

            Assert.True(problems.Count == 0,
                "machine-specific absolute paths (use res:// or a relative path):\n" + string.Join("\n", problems));
        }

        [Fact]
        public void ResReferences_ExistWithExactCase()
        {
            var root = RepoPaths.FindRepoRoot();
            var problems = new List<string>();

            foreach (var file in AssetFiles(root))
            {
                var modRoot = ModRootOf(file);
                var lines = File.ReadAllLines(Path.Combine(root, file));
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (Match match in ResReference.Matches(lines[i]))
                    {
                        var target = match.Groups[1].Value.Split("::")[0];
                        if (target.Contains('*') || target.Contains('{'))
                            continue;

                        var candidates = modRoot == null ? new[] { target } : new[] { target, $"{modRoot}/{target}" };
                        if (candidates.Any(c => RepoPaths.ExistsWithExactCase(root, c)))
                            continue;

                        var kind = candidates.Any(c => RepoPaths.ExistsIgnoringCase(root, c)) ? "wrong case" : "missing";
                        problems.Add($"{file}:{i + 1}: res://{target} ({kind})");
                    }
                }
            }

            Assert.True(problems.Count == 0, "broken res:// references:\n" + string.Join("\n", problems));
        }

        [Fact]
        public void ModManifestScenePaths_ExistWithExactCase()
        {
            var root = RepoPaths.FindRepoRoot();
            var problems = new List<string>();

            foreach (var manifest in RepoPaths.ProjectFiles(root)
                         .Where(f => Regex.IsMatch(f, @"^mods/[^/]+/manifest\.json$")))
            {
                var modRoot = ModRootOf(manifest);
                using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, manifest)));
                foreach (var section in new[] { "ui", "main_menu" })
                {
                    if (!json.RootElement.TryGetProperty(section, out var entries) || entries.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var entry in entries.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("scene_path", out var scenePath))
                            continue;

                        var relative = $"{modRoot}/{scenePath.GetString()}";
                        if (!RepoPaths.ExistsWithExactCase(root, relative))
                        {
                            var kind = RepoPaths.ExistsIgnoringCase(root, relative) ? "wrong case" : "missing";
                            problems.Add($"{manifest}: {section}.scene_path '{scenePath.GetString()}' ({kind})");
                        }
                    }
                }
            }

            Assert.True(problems.Count == 0, "broken manifest scene paths:\n" + string.Join("\n", problems));
        }

        [Fact]
        public void ProjectFiles_HaveNoCaseOnlyCollisions()
        {
            var root = RepoPaths.FindRepoRoot();
            var collisions = RepoPaths.ProjectFiles(root)
                .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(g => string.Join(" | ", g))
                .ToList();

            Assert.True(collisions.Count == 0,
                "paths that differ only by case collide on Windows/macOS:\n" + string.Join("\n", collisions));
        }

        private static IEnumerable<string> AssetFiles(string root)
            => RepoPaths.ProjectFiles(root)
                .Where(f => AssetExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

        /// <summary>Returns <c>mods/&lt;id&gt;</c> for files inside a mod, null otherwise.</summary>
        private static string ModRootOf(string file)
        {
            var parts = file.Split('/');
            return parts.Length > 2 && parts[0] == "mods" ? $"mods/{parts[1]}" : null;
        }
    }
}
