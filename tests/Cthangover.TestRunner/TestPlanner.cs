namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Closes prerequisites and component state while preserving explicit catalogue/access admission.</summary>
    public static class TestPlanner
    {
        /// <summary>Creates a nonempty plan before any workspace or child is started; external gates require exact selection.</summary>
        public static List<TestPlanEntry> Build(IReadOnlyList<IntegrationTestDefinition> catalog, RunnerOptions options)
        {
            var definitions = new Dictionary<string, IntegrationTestDefinition>(StringComparer.Ordinal);
            foreach (var test in catalog)
            {
                definitions.Add(test.Name, test);
            }

            var failed = options.Failed == null ? null : ReadFailed(options.Failed);
            ValidateSelectors(catalog, options);
            var result = new List<TestPlanEntry>();
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var test in catalog)
            {
                if (!Matches(test, catalog, options, failed))
                {
                    continue;
                }

                Add(test, "selected", definitions, selected, result, new HashSet<string>(StringComparer.Ordinal));
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("selection matched no runnable tests");
            }

            var chosen = new List<IntegrationTestDefinition>();
            foreach (var entry in result)
            {
                if (entry.Test.RequiresExplicitSelection && !options.Tests.Contains(entry.Test.Name, StringComparer.Ordinal))
                {
                    throw new ArgumentException(entry.Test.Name + ": exact --test selection is required, including prerequisites");
                }

                chosen.Add(entry.Test);
            }

            ProjectAccess.ValidateOutputs(chosen);
            var groups = new Dictionary<string, List<TestPlanEntry>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var entry in result)
            {
                var prior = failed != null && failed.TryGetValue(entry.Test.Name, out var previous) ? previous : default;
                entry.Filter = entry.Reason == "selected" ? options.Filter ?? prior.Filter : null;
                if (entry.Test.Kind == "unit" && options.Profile == "fast")
                {
                    entry.Filter = string.IsNullOrEmpty(entry.Filter) ? "FullyQualifiedName!~ToolsModCompileTests"
                        : "(" + entry.Filter + ")&FullyQualifiedName!~ToolsModCompileTests";
                }

                if (entry.Test.Name == "mod_units" && entry.Reason == "selected")
                {
                    entry.ModFilter = options.Mod ?? prior.Mod;
                }

                if (entry.Test.Name == "mod_units" && entry.Filter != null && entry.ModFilter == null)
                {
                    throw new ArgumentException("--filter with mod_units requires --mod; select a mod's cases explicitly");
                }

                entry.Environment = Component(entry.Test.Name, chosen);
                if (!groups.TryGetValue(entry.Environment, out var group))
                {
                    group = new List<TestPlanEntry>();
                    groups.Add(entry.Environment, group);
                    order.Add(entry.Environment);
                }

                group.Add(entry);
            }

            var ordered = new List<TestPlanEntry>();
            foreach (var environment in order)
            {
                ordered.AddRange(groups[environment]);
            }

            return ordered;
        }

        private static bool Matches(IntegrationTestDefinition test, IReadOnlyList<IntegrationTestDefinition> catalog,
            RunnerOptions options, Dictionary<string, (string Filter, string Mod)> failed)
        {
            var exact = options.Tests.Contains(test.Name, StringComparer.Ordinal);
            if (test.RequiresExplicitSelection && !exact)
            {
                return false;
            }

            var explicitName = false;
            foreach (var pattern in options.Tests)
            {
                explicitName |= NamePattern.Matches(test.Name, pattern);
            }

            var fromFailure = failed != null && failed.ContainsKey(test.Name);
            if (test.Tags.Contains("negative") && !exact && !fromFailure
                && options.Profile != "contract-negative" && options.Tag != "negative")
            {
                return false;
            }

            if (options.Tests.Count > 0 && !explicitName || failed != null && !fromFailure)
            {
                return false;
            }

            if (test.Kind == "manual")
            {
                if (explicitName)
                {
                    throw new ArgumentException(test.Name + ": " + test.ManualReason);
                }

                return false;
            }

            if (test.Tags.Contains("individual") && !explicitName && !fromFailure
                && options.Mod == null && options.Kind != "mod-unit")
            {
                return false;
            }

            if (test.Tags.Contains("individual") && !explicitName && options.Mod != null && options.Filter == null
                && HasScheduledVariant(catalog, test, options))
            {
                return false;
            }

            if (options.Mod != null && !test.Owners.Contains(options.Mod) && !(explicitName && test.Name == "mod_units")
                || options.Mod != null && test.Name == "mod_units" && !explicitName
                || options.Kind != null && test.Kind != options.Kind || options.Tag != null && !test.Tags.Contains(options.Tag)
                || options.Game != null && test.Game != options.Game || options.Renderer != null && test.Renderer != options.Renderer)
            {
                return false;
            }

            if (options.Filter != null && test.Kind != "unit" && test.Kind != "mod-unit"
                || options.Filter != null && options.Mod != null && options.Tests.Count == 0 && test.Name != "mod_units:" + options.Mod
                || options.Filter != null && options.Mod == null && options.Tests.Count == 0 && test.Kind != "unit")
            {
                return false;
            }

            var profile = options.Profile ?? (options.Tests.Count == 0 && options.Mod == null && failed == null
                && options.Kind == null && options.Tag == null ? "full" : null);
            return profile == null || test.Profiles.Contains(profile);
        }

        private static void ValidateSelectors(IReadOnlyList<IntegrationTestDefinition> catalog, RunnerOptions options)
        {
            foreach (var pattern in options.Tests)
            {
                var found = false;
                foreach (var test in catalog)
                {
                    found |= NamePattern.Matches(test.Name, pattern);
                }

                if (!found)
                {
                    throw new ArgumentException("unknown test pattern: " + pattern);
                }
            }

            var profile = options.Profile == null;
            var mod = options.Mod == null;
            foreach (var test in catalog)
            {
                if (options.Profile != null)
                {
                    profile |= test.Profiles.Contains(options.Profile);
                }
                if (options.Mod != null)
                {
                    mod |= test.Owners.Contains(options.Mod);
                }
            }

            if (!profile)
            {
                throw new ArgumentException("unknown profile: " + options.Profile);
            }

            if (!mod)
            {
                throw new ArgumentException("unknown mod in catalogue: " + options.Mod);
            }
        }

        private static void Add(IntegrationTestDefinition test, string reason,
            Dictionary<string, IntegrationTestDefinition> definitions, HashSet<string> selected,
            List<TestPlanEntry> result, HashSet<string> active)
        {
            if (selected.Contains(test.Name))
            {
                if (reason == "selected")
                {
                    foreach (var existing in result)
                    {
                        if (existing.Test.Name == test.Name)
                        {
                            existing.Reason = reason;
                        }
                    }
                }

                return;
            }

            if (!active.Add(test.Name))
            {
                throw new InvalidOperationException("dependency cycle at " + test.Name);
            }

            foreach (var name in test.DependsOn)
            {
                if (!definitions.TryGetValue(name, out var dependency))
                {
                    throw new InvalidOperationException("unknown dependency: " + name);
                }

                Add(dependency, "required by " + test.Name, definitions, selected, result, active);
            }

            active.Remove(test.Name);
            selected.Add(test.Name);
            result.Add(new TestPlanEntry
            {
                Test = test,
                Reason = reason
            });
        }

        private static string Component(string name, IReadOnlyList<IntegrationTestDefinition> catalog)
        {
            var group = new HashSet<string>(StringComparer.Ordinal) { name };
            bool changed;
            do
            {
                changed = false;
                foreach (var test in catalog)
                {
                    var connected = group.Contains(test.Name);
                    foreach (var dependency in test.DependsOn)
                    {
                        connected |= group.Contains(dependency);
                    }

                    if (!connected)
                    {
                        continue;
                    }

                    changed |= group.Add(test.Name);
                    foreach (var dependency in test.DependsOn)
                    {
                        changed |= group.Add(dependency);
                    }
                }
            } while (changed);

            var names = group.ToList();
            names.Sort(StringComparer.Ordinal);
            return names[0];
        }

        private static Dictionary<string, (string Filter, string Mod)> ReadFailed(string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
            var failures = new Dictionary<string, (string Filter, string Mod)>(StringComparer.Ordinal);
            foreach (var result in results)
            {
                if (result.GetProperty("status").GetString() is "fail" or "timeout" or "blocked" or "error")
                {
                    failures.Add(result.GetProperty("name").GetString(), ReadSelection(result));
                }
            }

            foreach (var result in results)
            {
                if (!failures.ContainsKey(result.GetProperty("name").GetString())
                    || !result.TryGetProperty("candidateLaunches", out var candidates))
                {
                    continue;
                }

                foreach (var candidate in candidates.EnumerateArray())
                {
                    var name = candidate.GetString();
                    foreach (var source in results)
                    {
                        if (source.GetProperty("name").GetString() == name)
                        {
                            failures.TryAdd(name, ReadSelection(source));
                            break;
                        }
                    }
                }
            }

            return failures;
        }

        private static (string Filter, string Mod) ReadSelection(JsonElement result)
        {
            return (result.TryGetProperty("filter", out var filter) ? filter.GetString() : null,
                result.TryGetProperty("modFilter", out var mod) ? mod.GetString() : null);
        }

        private static bool HasScheduledVariant(IReadOnlyList<IntegrationTestDefinition> catalog,
            IntegrationTestDefinition test, RunnerOptions options)
        {
            var mod = test.Name.Substring("mod_units:".Length);
            foreach (var other in catalog)
            {
                if (!other.Tags.Contains("individual") && other.Kind == "mod-unit"
                    && other.Args?.Contains("--mod=" + mod) == true
                    && (options.Profile == null || other.Profiles.Contains(options.Profile))
                    && (options.Tag == null || other.Tags.Contains(options.Tag))
                    && (options.Game == null || other.Game == options.Game)
                    && (options.Renderer == null || other.Renderer == options.Renderer))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
