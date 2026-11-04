namespace Cthangover.TestRunner
{
    /// <summary>Copies process-owned reports, sandboxes and images before the next component restores its workspace.</summary>
    public static class ArtifactCollector
    {
        public static void Collect(TestWorkspace workspace, string directory, TestResult result)
        {
            if (!result.Passed) result.Artifacts.AddRange(workspace.CaptureChanges(Path.Combine(directory, "changed-project")));
            var errors = Path.Combine(workspace.UserDirectory, "logs", "cthangover_errors.log");
            if (!result.Passed && File.Exists(errors))
                result.Diagnostics = File.ReadLines(errors).Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(12)
                    .Concat(result.Diagnostics).Distinct().Take(24).ToList();
            CopyTree(Path.Combine(workspace.UserDirectory, "logs"), Path.Combine(directory, "game-logs"), result.Artifacts);
            CopyTree(Path.Combine(workspace.UserDirectory, "mod_tests"), Path.Combine(directory, "mod-tests"), result.Artifacts);
            CopyTree(Path.Combine(workspace.UserDirectory, "mod_test_sandbox"), Path.Combine(directory, "sandbox"), result.Artifacts);
            CopyTree(Path.Combine(workspace.Root, "tests", "artifacts"), Path.Combine(directory, "outputs"), result.Artifacts);
            foreach (var item in result.Cases)
                item.Artifacts = item.Artifacts.Select(path => Relocate(path, workspace, directory)).ToList();
        }

        private static string Relocate(string path, TestWorkspace workspace, string directory)
        {
            var sandbox = Path.Combine(workspace.UserDirectory, "mod_test_sandbox");
            var relative = Path.GetRelativePath(sandbox, path);
            return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative)
                ? Path.Combine(directory, "sandbox", relative) : path;
        }

        private static void CopyTree(string source, string destination, List<string> artifacts)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("linked test artifact: " + file);
                var copy = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, copy, true);
                artifacts.Add(copy);
            }
            foreach (var child in Directory.EnumerateDirectories(source))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) throw new IOException("linked artifact directory: " + child);
                CopyTree(child, Path.Combine(destination, Path.GetFileName(child)), artifacts);
            }
        }
    }
}
