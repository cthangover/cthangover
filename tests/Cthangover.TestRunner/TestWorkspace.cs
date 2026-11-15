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
        private static readonly string[] Trees = new[] { "Core", "scenes", "collections", "config", "tests", "docs", ".opencode", ".godot", "sdk" }
            .Concat(ModFolders.Names).ToArray();
        private readonly Dictionary<string, string> originals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (long Length, DateTime Time, byte[] Text)> baseline = new(StringComparer.Ordinal);
        private readonly List<string> userDirectories = new();
        private readonly HashSet<string> directories = new(StringComparer.Ordinal);
        private readonly string runId;
        private readonly WorkspaceOwner workspaceOwner;
        private bool first = true;
        private TestWorkspace owner;
        private string dataHome;
        private bool shared;
        private readonly Dictionary<string, byte[]> sharedHashes = new(StringComparer.Ordinal);

        public string Root { get; }
        public string UserDirectory { get; private set; }
        public string TemporaryDirectory { get; private set; }
        public bool Keep { get; set; }
        public string CacheDirectory { get; }

        /// <summary>Copies project inputs; persistent compilation artifacts live outside the disposable run root.</summary>
        public TestWorkspace(string source, string runId, bool freshCache = false, string cacheRunId = null, string artifactsDirectory = null)
        {
            this.runId = runId;
            Root = Path.Combine(Path.GetTempPath(), "cthangover-tests", runId, "project");
            if (Directory.Exists(Root)) throw new IOException("workspace already exists: " + Root);
            CacheDirectory = CompilationCachePolicy.Resolve(Path.GetTempPath(), cacheRunId ?? runId, freshCache);
            workspaceOwner = new WorkspaceOwner(Path.GetDirectoryName(Root), runId, cacheRunId ?? runId, artifactsDirectory);
            try
            {
                Directory.CreateDirectory(Root);
                foreach (var file in Directory.EnumerateFiles(source))
                    if (Path.GetExtension(file) is ".godot" or ".csproj" or ".props" or ".targets" or ".sln" or ".cfg" or ".json" or ".md") Copy(file, Path.GetFileName(file));
                foreach (var tree in Trees)
                    if (Directory.Exists(Path.Combine(source, tree))) CopyTree(Path.Combine(source, tree), tree);
                PrepareOutputDirectories();
                foreach (var directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
                    directories.Add(Path.GetRelativePath(Root, directory).Replace('\\', '/'));
            }
            catch { workspaceOwner.Dispose(); throw; }
        }

        private TestWorkspace(TestWorkspace owner, string component)
        {
            this.owner = owner;
            runId = owner.runId;
            Root = owner.Root;
            CacheDirectory = owner.CacheDirectory;
            shared = true;
            dataHome = Path.Combine(Path.GetDirectoryName(Root), "homes", Key(component));
            var applicationData = OperatingSystem.IsMacOS() ? Path.Combine(dataHome, "Library", "Application Support") : dataHome;
            UserDirectory = Path.Combine(applicationData, "cthangover-tests", "data");
            if (Directory.Exists(dataHome)) throw new IOException("data home already exists: " + dataHome);
            Directory.CreateDirectory(Path.Combine(UserDirectory, "mods"));
            TemporaryDirectory = Path.Combine(Path.GetDirectoryName(Root), "temp", Key(component));
            Directory.CreateDirectory(TemporaryDirectory);
            lock (owner.userDirectories) owner.userDirectories.Add(UserDirectory);
        }

        /// <summary>Writes the sole shared override before launches start and makes it part of the immutable baseline.</summary>
        public void ConfigureShared()
        {
            shared = true;
            var path = Path.Combine(Root, "override.cfg");
            File.WriteAllText(path, "[application]\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name=\"cthangover-tests/data\"\n", new UTF8Encoding(false));
            var info = new FileInfo(path);
            baseline["override.cfg"] = (info.Length, info.LastWriteTimeUtc, File.ReadAllBytes(path));
            foreach (var entry in baseline)
                if (entry.Value.Text == null && !IgnoredForVerification(entry.Key))
                {
                    using var stream = File.OpenRead(Path.Combine(Root, entry.Key));
                    sharedHashes[entry.Key] = SHA256.HashData(stream);
                }
        }

        /// <summary>Shares immutable project/cache inputs while owning a private OS data home and temp directory.</summary>
        public TestWorkspace SharedComponent(string component)
        {
            if (!shared || owner != null) throw new InvalidOperationException("configure the shared owner before creating component data homes");
            return new TestWorkspace(this, component);
        }

        private void PrepareOutputDirectories()
        {
            if (!File.Exists(Path.Combine(Root, "tests", ModFolders.ManifestFile))) return;
            var manifest = TestManifest.LoadProject(Root);
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
        /// <param name="restore">Skip only when the caller proves the previous component did not mutate the project; user/temp roots are always fresh.</param>
        public void Begin(string component, bool restore = true)
        {
            if (shared) throw new InvalidOperationException("a shared project must never be restored or have its override rewritten");
            if (!first && restore)
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                RestoreCount++;
                try { RestoreProject(); }
                finally { RestoreSeconds += System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds; }
            }
            first = false;
            var relative = "cthangover-tests/" + runId + "/" + Key(component);
            var dataRoot = OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                : OperatingSystem.IsMacOS() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
                : Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            UserDirectory = Path.GetFullPath(Path.Combine(dataRoot, relative));
            if (Directory.Exists(UserDirectory)) throw new IOException("user-data directory already exists: " + UserDirectory);
            userDirectories.Add(UserDirectory);
            workspaceOwner.TrackUserDirectory(UserDirectory);
            Directory.CreateDirectory(Path.Combine(UserDirectory, "mods"));
            TemporaryDirectory = Path.Combine(Path.GetDirectoryName(Root), "temp", Key(component));
            Directory.CreateDirectory(TemporaryDirectory);
            File.WriteAllText(Path.Combine(Root, "override.cfg"),
                "[application]\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name=\"" + relative + "\"\n", new UTF8Encoding(false));
        }

        public Dictionary<string, string> EnvironmentVariables()
        {
            var variables = new Dictionary<string, string>
            {
            ["CTHANGOVER_TEST_DATA"] = UserDirectory,
            ["CTHANGOVER_TEST_CACHE"] = CacheDirectory,
            ["CTHANGOVER_TEST_PROJECT"] = Root,
            ["TMP"] = TemporaryDirectory, ["TEMP"] = TemporaryDirectory, ["TMPDIR"] = TemporaryDirectory,
            };
            if (dataHome != null)
            {
                var variable = OperatingSystem.IsWindows() ? "APPDATA" : OperatingSystem.IsMacOS() ? "HOME" : "XDG_DATA_HOME";
                variables[variable] = dataHome;
                // HOME isolation on macOS must not hide the installed NuGet/code-generator cache from dotnet or Godot.
                variables["NUGET_PACKAGES"] = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
                variables["DOTNET_CLI_HOME"] = Path.Combine(dataHome, "dotnet");
                variables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
            }
            return variables;
        }

        /// <summary>Total time spent restoring this copy; unchanged-size/timestamp text still receives byte verification.</summary>
        public double RestoreSeconds { get; private set; }
        /// <summary>Actual restoration attempts; skipped restores and the first component are not counted.</summary>
        public int RestoreCount { get; private set; }

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
                ".cs" or ".gd" or ".json" or ".tscn" or ".tres" or ".scenario" or ".properties" or ".md" or ".cfg" or ".godot" or ".props" or ".targets"
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
            if (owner != null) return; // The shared owner disposes all component homes with its run directory.
            try
            {
                if (Keep) return;
                foreach (var directory in userDirectories)
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                foreach (var directory in userDirectories.Select(Path.GetDirectoryName).Distinct())
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                var parent = Path.GetDirectoryName(Root);
                workspaceOwner.ReleaseLease();
                if (Directory.Exists(parent)) Directory.Delete(parent, true);
            }
            finally { workspaceOwner.Dispose(); }
        }

        /// <summary>Finds additions, deletions and metadata/byte changes without restoring a snapshot still used by readers.</summary>
        public List<string> VerifyUnchanged()
        {
            var changes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
                if (IgnoredForVerification(relative)) continue;
                if (baseline.TryGetValue(relative, out var original))
                {
                    var info = new FileInfo(file);
                    if (info.Length == original.Length && info.LastWriteTimeUtc == original.Time &&
                        (original.Text == null || File.ReadAllBytes(file).AsSpan().SequenceEqual(original.Text)))
                    {
                        if (!sharedHashes.TryGetValue(relative, out var hash)) continue;
                        using var stream = File.OpenRead(file);
                        if (SHA256.HashData(stream).AsSpan().SequenceEqual(hash)) continue;
                    }
                }
                changes.Add(relative);
            }
            foreach (var relative in baseline.Keys)
                if (!IgnoredForVerification(relative) && !File.Exists(Path.Combine(Root, relative))) changes.Add(relative);
            return changes.OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        private static bool IgnoredForVerification(string relative) => relative.StartsWith(".godot/", StringComparison.Ordinal)
            || relative.StartsWith("tests/artifacts/", StringComparison.Ordinal);

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
                        (original.Text == null || File.ReadAllBytes(file).AsSpan().SequenceEqual(original.Text)))
                    {
                        if (!sharedHashes.TryGetValue(relative, out var hash)) continue;
                        using var stream = File.OpenRead(file);
                        if (SHA256.HashData(stream).AsSpan().SequenceEqual(hash)) continue;
                    }
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
