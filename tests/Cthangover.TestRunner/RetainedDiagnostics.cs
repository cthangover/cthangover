namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Copies only textual failure evidence before eviction, never the project; oversized logs retain a bounded tail.</summary>
    public static class RetainedDiagnostics
    {
        private const long FileLimit = 8 * 1024 * 1024;
        private const long TotalLimit = 64 * 1024 * 1024;

        /// <summary>Archives pending user logs/results; stdout/stderr already stream directly into the main report directory.</summary>
        public static void Save(string root, WorkspaceOwnerInfo owner, IEnumerable<string> userDirectories)
        {
            if (string.IsNullOrWhiteSpace(owner.ArtifactsDirectory)) return;
            var destination = Path.Combine(owner.ArtifactsDirectory, "retained-diagnostics", owner.RunId);
            if (WorkspaceOwner.IsWithin(destination, root)) throw new IOException("diagnostic report is inside the evicted workspace");
            var sources = new[] { Path.Combine(root, "homes"), Path.Combine(root, "temp") }.Concat(userDirectories).ToArray();
            var manifest = new List<string>();
            long copied = 0;
            for (var index = 0; index < sources.Length; index++)
            {
                foreach (var file in Files(sources[index]))
                {
                    var name = Path.GetFileName(file);
                    var extension = Path.GetExtension(name);
                    var archive = Path.GetFileNameWithoutExtension(name).EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                        && extension.Length > 1 && int.TryParse(extension.AsSpan(1), System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out var indexNumber) && indexNumber > 0;
                    if (!(archive || name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".result", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".result.json", StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("report.json", StringComparison.OrdinalIgnoreCase))) continue;
                    var info = new FileInfo(file);
                    var length = Math.Min(info.Length, FileLimit);
                    if (copied + length > TotalLimit) throw new IOException("retained diagnostic budget exceeded; workspace preserved");
                    var target = Path.Combine(destination, index.ToString(), Path.GetRelativePath(sources[index], file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using var input = File.OpenRead(file);
                    input.Seek(info.Length - length, SeekOrigin.Begin);
                    using var output = File.Create(target);
                    input.CopyTo(output);
                    copied += length;
                    manifest.Add(file + " -> " + target + (length == info.Length ? "" : " (last 8 MiB; truncated)"));
                }
            }
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "manifest.json"), JsonSerializer.Serialize(manifest));
            File.WriteAllText(Path.Combine(destination, "owner.json"), JsonSerializer.Serialize(owner));
        }

        private static IEnumerable<string> Files(string root)
        {
            if (!Directory.Exists(root)) yield break;
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("linked diagnostic root");
            foreach (var path in Directory.EnumerateFileSystemEntries(root))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                if ((attributes & FileAttributes.Directory) == 0) yield return path;
                else foreach (var file in Files(path)) yield return file;
            }
        }
    }
}
