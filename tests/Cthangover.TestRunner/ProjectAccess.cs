namespace Cthangover.TestRunner
{
    /// <summary>Validates writable output scopes before any children can share a project snapshot.</summary>
    public static class ProjectAccess
    {
        /// <summary>Extracts canonical project-relative output directories from argument values; traversal is rejected.</summary>
        public static string[] OutputDirectories(IntegrationTestDefinition test)
        {
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var argument in test.Args ?? Array.Empty<string>())
            {
                var value = argument.Substring(argument.IndexOf('=') + 1).Replace('\\', '/');
                if (!value.StartsWith("res://tests/artifacts/", StringComparison.Ordinal)) continue;
                var relative = value.Substring("res://".Length).TrimEnd('/');
                if (relative == "tests/artifacts" || relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains(':'))
                    throw new InvalidOperationException(test.Name + ": invalid output directory " + value);
                directories.Add(relative);
            }
            return directories.ToArray();
        }

        /// <summary>Missing access metadata remains conservative; explicit unknown values never silently become writable.</summary>
        public static void Validate(IntegrationTestDefinition test)
        {
            if (test.Project is not ("read" or "output" or "write"))
                throw new InvalidOperationException(test.Name + ": unknown project access '" + test.Project + "'");
            if (test.Seconds.HasValue && (!double.IsFinite(test.Seconds.Value) || test.Seconds <= 0))
                throw new InvalidOperationException(test.Name + ": seconds must be positive and finite");
            if (test.Project == "output" && OutputDirectories(test).Length == 0)
                throw new InvalidOperationException(test.Name + ": output access requires a res://tests/artifacts/ argument");
        }

        /// <summary>Sibling outputs are independent; identical or nested scopes require one dependency component.</summary>
        public static void ValidateOutputs(IReadOnlyList<IntegrationTestDefinition> tests)
        {
            var components = tests.ToDictionary(test => test.Name, test => test.Name, StringComparer.Ordinal);
            components.TryAdd("unit", "unit");
            foreach (var test in tests)
                foreach (var dependency in test.DependsOn)
                {
                    var from = components[test.Name];
                    var to = components[dependency];
                    foreach (var name in components.Keys.ToArray())
                        if (components[name] == from) components[name] = to;
                }
            for (var i = 0; i < tests.Count; i++)
                for (var j = i + 1; j < tests.Count; j++)
                {
                    if (components[tests[i].Name] == components[tests[j].Name]) continue;
                    foreach (var left in OutputDirectories(tests[i]))
                        foreach (var right in OutputDirectories(tests[j]))
                            if (left.Equals(right, StringComparison.OrdinalIgnoreCase)
                                || left.StartsWith(right + "/", StringComparison.OrdinalIgnoreCase)
                                || right.StartsWith(left + "/", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException($"shared output directory: {tests[i].Name} ({left}) and {tests[j].Name} ({right})");
                }
        }
    }
}
