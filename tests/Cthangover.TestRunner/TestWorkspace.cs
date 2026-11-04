namespace Cthangover.TestRunner
{
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Owns a physical project copy and private user/temp directories. Assets are copied, never linked to writable sources.
    /// Dependency components share data; the project is restored before the next component. Failed workspaces are retained.
    /// </summary>
    public sealed class TestWorkspace : IDisposable
    {
        private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".git-template", ".idea", "node_modules", "__pycache__" };
        private static readonly string[] Trees = { "Core", "mods", "scenes", "collections", "config", "tests", "docs", ".opencode", ".godot", "sdk" };
        private readonly Dictionary<string, string> originals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (long Length, DateTime Time, byte[] Text)> baseline = new(StringComparer.Ordinal);
        private readonly List<string> userDirectories = new();
        private readonly HashSet<string> directories = new(StringComparer.Ordinal);
        private readonly string runId;
        private bool first = true;

        public string Root { get; }
        public string UserDirectory { get; private set; }
        public string TemporaryDirectory { get; private set; }
        public bool Keep { get; set; }
        public string CacheDirectory { get; }

        public TestWorkspace(string source, string runId)
        {
            this.runId = runId;
            Root = Path.Combine(Path.GetTempPath(), "cthangover-tests", runId, "project");
            if (Directory.Exists(Root)) throw new IOException("workspace already exists: " + Root);
            CacheDirectory = Path.Combine(Path.GetDirectoryName(Root), "cache", "assemblies");
            Directory.CreateDirectory(Root);
            foreach (var file in Directory.EnumerateFiles(source))
                if (Path.GetExtension(file) is ".godot" or ".csproj" or ".sln" or ".cfg" or ".json" or ".md") Copy(file, Path.GetFileName(file));
            foreach (var tree in Trees)
                if (Directory.Exists(Path.Combine(source, tree))) CopyTree(Path.Combine(source, tree), tree);
            PrepareOutputDirectories();
            foreach (var directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
                directories.Add(Path.GetRelativePath(Root, directory).Replace('\\', '/'));
        }

        private void PrepareOutputDirectories()
        {
            var path = Path.Combine(Root, "tests", "tests.manifest.json");
            if (!File.Exists(path)) return;
            var manifest = TestManifest.Load(path);
            foreach (var argument in manifest.Integration.SelectMany(test => test.Args))
            {
                var separator = argument.IndexOf('=');
                if (separator < 0) continue;
                var value = argument.Substring(separator + 1);
                if (!value.StartsWith("res://tests/artifacts/", StringComparison.Ordinal)) continue;
                var destination = Path.GetFullPath(Path.Combine(Root, value.Substring("res://".Length)));
                if (!Path.GetRelativePath(Root, destination).StartsWith("..", StringComparison.Ordinal)) Directory.CreateDirectory(destination);
            }
        }

        public static string Key(string name)
        {
            var readable = new string(name.Select(c => char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_').Take(48).ToArray());
            return readable + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))).Substring(0, 8).ToLowerInvariant();
        }

        /// <summary>Prepares a component's fresh data root; calling this again starts a new independent component.</summary>
        public void Begin(string component)
        {
            if (!first) RestoreProject();
            first = false;
            var relative = "cthangover-tests/" + runId + "/" + Key(component);
            var dataRoot = OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                : OperatingSystem.IsMacOS() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
                : Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            UserDirectory = Path.GetFullPath(Path.Combine(dataRoot, relative));
            if (Directory.Exists(UserDirectory)) throw new IOException("user-data directory already exists: " + UserDirectory);
            userDirectories.Add(UserDirectory);
            Directory.CreateDirectory(Path.Combine(UserDirectory, "mods"));
            TemporaryDirectory = Path.Combine(Path.GetDirectoryName(Root), "temp", Key(component));
            Directory.CreateDirectory(TemporaryDirectory);
            File.WriteAllText(Path.Combine(Root, "override.cfg"),
                "[application]\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name=\"" + relative + "\"\n", new UTF8Encoding(false));
        }

        public Dictionary<string, string> EnvironmentVariables() => new()
        {
            ["CTHANGOVER_TEST_DATA"] = UserDirectory,
            ["CTHANGOVER_TEST_CACHE"] = CacheDirectory,
            ["CTHANGOVER_TEST_PROJECT"] = Root,
            ["TMP"] = TemporaryDirectory, ["TEMP"] = TemporaryDirectory, ["TMPDIR"] = TemporaryDirectory,
        };

        private void CopyTree(string source, string relative)
        {
            Directory.CreateDirectory(Path.Combine(Root, relative));
            foreach (var file in Directory.EnumerateFiles(source)) Copy(file, Path.Combine(relative, Path.GetFileName(file)));
            foreach (var child in Directory.EnumerateDirectories(source))
            {
                var name = Path.GetFileName(child);
                var next = Path.Combine(relative, name).Replace('\\', '/');
                if (Excluded.Contains(name) || next.StartsWith(".godot/editor", StringComparison.Ordinal)
                    || next == "tests/artifacts"
                    || next.StartsWith(".godot/mono/temp/obj", StringComparison.Ordinal)
                    || next.StartsWith(".godot/mono/temp/bin/Export", StringComparison.Ordinal)
                    || next.StartsWith("tests/Cthangover.TestRunner/", StringComparison.Ordinal)
                    || next.Contains("/obj/", StringComparison.Ordinal) || name == "obj") continue;
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("workspace source contains a linked directory: " + child);
                CopyTree(child, next);
            }
        }

        private void Copy(string source, string relative)
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("workspace source contains a linked file: " + source);
            var destination = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination);
            File.SetAttributes(destination, FileAttributes.Normal);
            originals.Add(relative.Replace('\\', '/'), source);
            var info = new FileInfo(destination);
            // Immutable in-memory baselines make even same-size/same-timestamp edits of authoring files visible.
            var text = info.Length <= 4 * 1024 * 1024 && Path.GetExtension(source).ToLowerInvariant() is
                ".cs" or ".gd" or ".json" or ".tscn" or ".tres" or ".scenario" or ".properties" or ".md" or ".cfg" or ".godot"
                ? File.ReadAllBytes(destination) : null;
            baseline.Add(relative.Replace('\\', '/'), (info.Length, info.LastWriteTimeUtc, text));
        }

        private void RestoreProject()
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
                if (!originals.ContainsKey(relative)) File.Delete(file);
            }
            foreach (var directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                if (!directories.Contains(Path.GetRelativePath(Root, directory).Replace('\\', '/')) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            foreach (var directory in directories) Directory.CreateDirectory(Path.Combine(Root, directory));
            foreach (var entry in originals)
            {
                var destination = Path.Combine(Root, entry.Key);
                var original = baseline[entry.Key];
                var info = new FileInfo(destination);
                var changed = !info.Exists || info.Length != original.Length || info.LastWriteTimeUtc != original.Time;
                if (!changed && original.Text != null) changed = !File.ReadAllBytes(destination).AsSpan().SequenceEqual(original.Text);
                if (!changed) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (original.Text != null) File.WriteAllBytes(destination, original.Text);
                else File.Copy(entry.Value, destination, true);
                File.SetLastWriteTimeUtc(destination, original.Time);
            }
        }

        public void Dispose()
        {
            if (Keep) return;
            foreach (var directory in userDirectories)
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            foreach (var directory in userDirectories.Select(Path.GetDirectoryName).Distinct())
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            var parent = Path.GetDirectoryName(Root);
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }

        /// <summary>Preserves failed authoring edits before a later independent component restores the working copy.</summary>
        public List<string> CaptureChanges(string destination)
        {
            var artifacts = new List<string>();
            var removed = new List<string>();
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
                if (relative.StartsWith(".godot/", StringComparison.Ordinal) || relative.StartsWith("tests/artifacts/", StringComparison.Ordinal)) continue;
                if (baseline.TryGetValue(relative, out var original))
                {
                    var info = new FileInfo(file);
                    if (info.Length == original.Length && info.LastWriteTimeUtc == original.Time &&
                        (original.Text == null || File.ReadAllBytes(file).AsSpan().SequenceEqual(original.Text))) continue;
                }
                var copy = Path.GetFullPath(Path.Combine(destination, relative));
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(file, copy, true);
                artifacts.Add(copy);
            }
            foreach (var relative in originals.Keys)
                if (!File.Exists(Path.Combine(Root, relative))) removed.Add(relative);
            if (removed.Count > 0)
            {
                Directory.CreateDirectory(destination);
                var path = Path.Combine(destination, "deleted-files.json");
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(removed));
                artifacts.Add(path);
            }
            return artifacts;
        }
    }
}
