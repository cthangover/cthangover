namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Filters the catalogue, closes prerequisites and groups connected tests into shared isolated environments.</summary>
    public static class TestPlanner
    {
        public static List<TestPlanEntry> Build(IReadOnlyList<IntegrationTestDefinition> catalog, RunnerOptions options)
        {
            var definitions = catalog.ToDictionary(t => t.Name, StringComparer.Ordinal);
            var failed = options.Failed == null ? null : ReadFailed(options.Failed);
            foreach (var pattern in options.Tests)
                if (!catalog.Any(t => NamePattern.Matches(t.Name, pattern))) throw new ArgumentException("unknown test pattern: " + pattern);
            if (options.Profile != null && !catalog.Any(t => t.Profiles.Contains(options.Profile))) throw new ArgumentException("unknown profile: " + options.Profile);
            if (options.Mod != null && !catalog.Any(t => t.Owners.Contains(options.Mod))) throw new ArgumentException("unknown mod in catalogue: " + options.Mod);
            var result = new List<TestPlanEntry>();
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var test in catalog)
            {
                var explicitName = options.Tests.Any(p => NamePattern.Matches(test.Name, p));
                var fromFailure = failed != null && failed.ContainsKey(test.Name);
                if (test.Tags.Contains("negative") && !options.Tests.Contains(test.Name) && !fromFailure && options.Profile != "contract-negative" && options.Tag != "negative") continue;
                if (options.Tests.Count > 0 && !explicitName) continue;
                if (failed != null && !failed.ContainsKey(test.Name)) continue;
                if (test.Kind == "manual")
                {
                    if (explicitName) throw new ArgumentException(test.Name + ": " + test.ManualReason);
                    continue;
                }
                if (test.Tags.Contains("individual") && !explicitName && !fromFailure && options.Mod == null && options.Kind != "mod-unit") continue;
                if (test.Tags.Contains("individual") && !explicitName && options.Mod != null && options.Filter == null && HasScheduledVariant(catalog, test, options)) continue;
                if (options.Mod != null && !test.Owners.Contains(options.Mod) && !(explicitName && test.Name == "mod_units")) continue;
                if (options.Mod != null && test.Name == "mod_units" && !explicitName) continue;
                if (options.Kind != null && test.Kind != options.Kind) continue;
                if (options.Tag != null && !test.Tags.Contains(options.Tag)) continue;
                if (options.Game != null && test.Game != options.Game) continue;
                if (options.Renderer != null && test.Renderer != options.Renderer) continue;
                if (options.Filter != null && test.Kind != "unit" && test.Kind != "mod-unit") continue;
                if (options.Filter != null && options.Mod != null && options.Tests.Count == 0 && test.Name != "mod_units:" + options.Mod) continue;
                if (options.Filter != null && options.Mod == null && options.Tests.Count == 0 && test.Kind != "unit") continue;
                var profile = options.Profile ?? (options.Tests.Count == 0 && options.Mod == null && failed == null && options.Kind == null && options.Tag == null ? "full" : null);
                if (profile != null && !test.Profiles.Contains(profile)) continue;
                Add(test, "selected", definitions, selected, result, new HashSet<string>(StringComparer.Ordinal));
            }
            if (result.Count == 0) throw new ArgumentException("selection matched no runnable tests");
            var chosen = result.Select(entry => entry.Test).ToList();
            foreach (var entry in result)
            {
                var previous = failed != null && failed.TryGetValue(entry.Test.Name, out var prior) ? prior : default;
                entry.Filter = entry.Reason == "selected" ? options.Filter ?? previous.Filter : null;
                if (entry.Test.Kind == "unit" && options.Profile == "fast")
                    entry.Filter = string.IsNullOrEmpty(entry.Filter) ? "FullyQualifiedName!~ToolsModCompileTests" : "(" + entry.Filter + ")&FullyQualifiedName!~ToolsModCompileTests";
                if (entry.Test.Name == "mod_units" && entry.Reason == "selected") entry.ModFilter = options.Mod ?? previous.Mod;
                if (entry.Test.Name == "mod_units" && entry.Filter != null && entry.ModFilter == null)
                    throw new ArgumentException("--filter with mod_units requires --mod; select a mod's cases explicitly");
                entry.Environment = Component(entry.Test.Name, chosen);
            }
            return result.GroupBy(e => e.Environment).SelectMany(group => group).ToList();
        }

        private static void Add(IntegrationTestDefinition test, string reason, Dictionary<string, IntegrationTestDefinition> definitions,
            HashSet<string> selected, List<TestPlanEntry> result, HashSet<string> active)
        {
            if (selected.Contains(test.Name))
            {
                if (reason == "selected") result.Single(entry => entry.Test.Name == test.Name).Reason = reason;
                return;
            }
            if (!active.Add(test.Name)) throw new InvalidOperationException("dependency cycle at " + test.Name);
            foreach (var name in test.DependsOn)
            {
                if (!definitions.TryGetValue(name, out var dependency)) throw new InvalidOperationException("unknown dependency: " + name);
                Add(dependency, "required by " + test.Name, definitions, selected, result, active);
            }
            active.Remove(test.Name);
            selected.Add(test.Name);
            result.Add(new TestPlanEntry { Test = test, Reason = reason });
        }

        private static string Component(string name, IReadOnlyList<IntegrationTestDefinition> catalog)
        {
            var group = new HashSet<string>(StringComparer.Ordinal) { name };
            bool changed;
            do
            {
                changed = false;
                foreach (var test in catalog)
                    if (group.Contains(test.Name) || test.DependsOn.Any(group.Contains))
                    {
                        changed |= group.Add(test.Name);
                        foreach (var dependency in test.DependsOn) changed |= group.Add(dependency);
                    }
            } while (changed);
            return group.OrderBy(n => n, StringComparer.Ordinal).First();
        }

        private static Dictionary<string, (string Filter, string Mod)> ReadFailed(string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.GetProperty("results").EnumerateArray()
                .Where(r => r.GetProperty("status").GetString() is "fail" or "timeout" or "blocked" or "error")
                .ToDictionary(r => r.GetProperty("name").GetString(), r => (
                    Filter: r.TryGetProperty("filter", out var filter) ? filter.GetString() : null,
                    Mod: r.TryGetProperty("modFilter", out var mod) ? mod.GetString() : null), StringComparer.Ordinal);
        }

        private static bool HasScheduledVariant(IReadOnlyList<IntegrationTestDefinition> catalog, IntegrationTestDefinition test, RunnerOptions options)
        {
            var mod = test.Name.Substring("mod_units:".Length);
            return catalog.Any(other => !other.Tags.Contains("individual") && other.Kind == "mod-unit" && other.Args?.Contains("--mod=" + mod) == true
                && (options.Profile == null || other.Profiles.Contains(options.Profile)) && (options.Tag == null || other.Tags.Contains(options.Tag))
                && (options.Game == null || other.Game == options.Game) && (options.Renderer == null || other.Renderer == options.Renderer));
        }
    }
}
