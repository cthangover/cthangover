namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Joins scheduled launches, per-mod unit suites and explicitly manual suites into one validated catalogue.</summary>
    public static class TestCatalog
    {
        public static List<IntegrationTestDefinition> Load(string root, TestManifest manifest)
        {
            var entries = new List<IntegrationTestDefinition>
            {
                new() { Name = "unit", Kind = "unit", Owners = new[] { "core" }, Profiles = new[] { "full", "fast", "headless" },
                    Tags = new[] { "core", "xunit" }, Source = manifest.Unit.Project, TimeoutSeconds = manifest.Unit.TimeoutSeconds,
                    Project = "read", Seconds = manifest.Unit.Seconds }
            };
            entries.AddRange(manifest.Integration);
            var sceneRoot = Path.Combine(root, "tests", "Cthangover.Tests.Integration", "Scenes");
            if (Directory.Exists(sceneRoot))
                foreach (var scene in Directory.EnumerateFiles(sceneRoot, "*.tscn"))
                {
                    var relative = Path.GetRelativePath(root, scene).Replace('\\', '/');
                    if (manifest.Integration.Any(test => test.Scene == "res://" + relative)) continue;
                    if (!manifest.ManualScenes.TryGetValue(relative, out var reason) || string.IsNullOrWhiteSpace(reason))
                        throw new InvalidOperationException("scene has no scheduled test or manualScenes reason: " + relative);
                    entries.Add(new IntegrationTestDefinition { Name = "manual:scene:" + Path.GetFileNameWithoutExtension(scene), Kind = "manual",
                        Source = relative, Scene = "res://" + relative, Owners = new[] { "core" }, Tags = new[] { "manual" }, ManualReason = reason });
                }
            var policies = new HashSet<string>(manifest.ManualSuites.Keys, StringComparer.Ordinal);
            foreach (var dir in ModFolders.Names.Select(folder => Path.Combine(root, folder)).Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateDirectories(folder).OrderBy(p => p, StringComparer.Ordinal)))
            {
                var path = Path.Combine(dir, "manifest.json");
                if (!File.Exists(path)) continue;
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var data = document.RootElement;
                var id = data.GetProperty("id").GetString();
                AddUnits(entries, data, id, Path.GetRelativePath(root, path), new[] { "--", "--all-mods", "--with-mods=" + id });
                if (data.TryGetProperty("integrations", out var integrations))
                    foreach (var integration in integrations.EnumerateArray())
                    {
                        var part = integration.GetProperty("id").GetString();
                        var folder = integration.TryGetProperty("path", out var customPath) && !string.IsNullOrWhiteSpace(customPath.GetString())
                            ? customPath.GetString().Trim().TrimEnd('/') : "integrations/" + part;
                        var childPath = Path.Combine(dir, folder, "manifest.json");
                        using var child = JsonDocument.Parse(File.ReadAllText(childPath));
                        AddUnits(entries, child.RootElement, id + "__" + part, Path.GetRelativePath(root, childPath),
                            new[] { "--", "--all-mods", "--with-mods=" + id });
                    }
                if (!data.TryGetProperty("test_suites", out var suites)) continue;
                foreach (var suite in suites.EnumerateArray())
                {
                    var part = suite.GetProperty("id").GetString();
                    var virtualId = id + "__" + part;
                    var folder = suite.TryGetProperty("path", out var custom) && !string.IsNullOrWhiteSpace(custom.GetString())
                        ? custom.GetString().Trim().TrimEnd('/') : "test_suites/" + part;
                    var childPath = Path.Combine(dir, folder, "manifest.json");
                    if (!File.Exists(childPath)) throw new InvalidOperationException("suite manifest missing: " + childPath);
                    var activate = suite.TryGetProperty("activate", out var flags)
                        ? flags.EnumerateArray().Select(f => f.GetString()).ToArray() : Array.Empty<string>();
                    var scheduled = manifest.Integration.Any(t => t.Args.Any(a => activate.Any(prefix => a == prefix || prefix.EndsWith('=') && a.StartsWith(prefix, StringComparison.Ordinal))));
                    using var suiteDocument = JsonDocument.Parse(File.ReadAllText(childPath));
                    var native = manifest.Integration.FirstOrDefault(test => test.Kind == "mod-unit" && test.Args.Contains("--mod=" + virtualId));
                    if (native != null)
                    {
                        var entry = AddUnits(entries, suiteDocument.RootElement, virtualId, Path.GetRelativePath(root, childPath),
                            native.Args.Where(arg => arg != "--run-tests" && !arg.StartsWith("--mod=", StringComparison.Ordinal)).ToArray());
                        if (entry != null)
                        {
                            entry.Windowed = native.Windowed;
                            entry.Game = native.Game;
                            entry.Renderer = native.Renderer;
                            entry.TimeoutSeconds = native.TimeoutSeconds;
                            entry.RequiresExplicitSelection = native.RequiresExplicitSelection;
                            entry.Profiles = native.Profiles;
                            entry.Project = native.Project;
                            entry.Seconds = native.Seconds;
                        }
                    }
                    if (!scheduled)
                    {
                        if (!manifest.ManualSuites.TryGetValue(virtualId, out var policy) || string.IsNullOrWhiteSpace(policy?.Reason))
                            throw new InvalidOperationException($"suite '{virtualId}' has no scheduled test or manualSuites reason");
                        policies.Remove(virtualId);
                        entries.Add(new IntegrationTestDefinition { Name = "manual:" + virtualId, Kind = "manual", Owners = new[] { id },
                            Source = Path.GetRelativePath(root, childPath), ManualReason = policy.Reason, Args = activate, Tags = new[] { "manual" },
                            Windowed = policy.Windowed, Game = policy.Game, Requirements = policy.Requirements });
                    }
                }
            }
            if (policies.Count > 0) throw new InvalidOperationException("unused manual suite policy: " + string.Join(", ", policies));
            if (entries.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count() != entries.Count)
                throw new InvalidOperationException("duplicate catalogue name");
            return entries;
        }

        private static IntegrationTestDefinition AddUnits(List<IntegrationTestDefinition> entries, JsonElement manifest, string id, string path, string[] args)
        {
            if (!manifest.TryGetProperty("test_sources", out var sources) || sources.GetArrayLength() == 0) return null;
            var entry = new IntegrationTestDefinition
            {
                Name = "mod_units:" + id, Kind = "mod-unit", Scene = "res://scenes/menu/launcher.tscn",
                Args = args.Concat(new[] { "--run-tests", "--mod=" + id }).ToArray(), TimeoutSeconds = 300,
                Owners = id.Contains("__", StringComparison.Ordinal) ? new[] { id.Split("__")[0], id } : new[] { id },
                Profiles = new[] { "full", "fast", "headless" }, Tags = new[] { "individual", "mod-unit" },
                Source = path, Result = new ResultContract { Name = "mod_units", ChildPrefix = "mod_units:" },
            };
            entries.Add(entry);
            return entry;
        }

        public static void Write(string path, IReadOnlyList<IntegrationTestDefinition> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = 2, tests = entries },
                new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
