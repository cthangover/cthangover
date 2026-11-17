namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Evicts inactive logical runs by age, newest-count and size; every lane of an active run remains protected.</summary>
    public static class WorkspaceRetention
    {
        /// <summary>Prunes only owner-marked copies. Unrecognised legacy folders remain the process-aware container cleaner's responsibility.</summary>
        public static IReadOnlyList<string> Prune(string temporaryRoot, int maxCount, double maxDays, long maxBytes,
            DateTimeOffset? now = null, IEnumerable<string> dataHomes = null)
        {
            if (maxCount < 0 || maxDays < 0 || !double.IsFinite(maxDays) || maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            var root = Path.Combine(temporaryRoot, "cthangover-tests");
            if (!Directory.Exists(root)) return Array.Empty<string>();
            var homes = (dataHomes ?? DefaultHomes()).Select(Path.GetFullPath).ToArray();
            var copies = new List<RetainedWorkspace>();
            var active = new HashSet<string>(StringComparer.Ordinal);
            var deleted = new List<string>();
            FileStream gate;
            try { gate = new FileStream(Path.Combine(root, ".retention.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { return deleted; }
            using (gate)
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    if (Path.GetFileName(directory) == "cache" || IsLinked(directory)) continue;
                    var marker = Path.Combine(directory, ".owner.json");
                    if (!File.Exists(marker)) continue;
                    var group = System.Text.RegularExpressions.Regex.Replace(Path.GetFileName(directory), "_(shared|write[0-9]+)$", "");
                    if (File.Exists(Path.Combine(directory, ".process-unconfirmed.json")))
                    {
                        // Root exit does not prove inherited-pipe descendants released component DATA.
                        active.Add(group);
                        continue;
                    }
                    FileStream lease = null;
                    try
                    {
                        var owner = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(marker));
                        if (owner == null || owner.RunId != Path.GetFileName(directory) || owner.GroupId != group)
                            throw new IOException("invalid workspace owner marker");
                        if (WorkspaceOwner.IsAlive(owner)) { active.Add(group); continue; }
                        lease = new FileStream(Path.Combine(directory, ".owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        // Re-read after acquiring the lease; another owner cannot replace an active marker under us.
                        var latest = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(marker));
                        if (latest == null) throw new IOException("empty workspace owner marker");
                        if (latest.Active || latest.UpdatedAt != owner.UpdatedAt)
                        {
                            if (latest.Active && WorkspaceOwner.IsAlive(latest)) { active.Add(group); lease.Dispose(); continue; }
                            if (latest.UpdatedAt != owner.UpdatedAt) throw new IOException("workspace owner changed");
                        }
                        var copy = new RetainedWorkspace { Root = directory, Owner = owner, Lease = lease };
                        foreach (var home in homes)
                        {
                            var data = Path.Combine(home, "cthangover-tests", owner.RunId);
                            if (Directory.Exists(data)) copy.UserRoots.Add(data);
                        }
                        copy.Bytes = Measure(directory) + copy.UserRoots.Sum(Measure);
                        copies.Add(copy); lease = null;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
                    {
                        active.Add(group); // Unknown ownership preserves all lanes in the group.
                        RunnerConsole.WriteLine("Retention skipped " + directory + ": " + error.Message);
                    }
                    finally { lease?.Dispose(); }
                }
                var groups = copies.Where(c => !active.Contains(c.Owner.GroupId)).GroupBy(c => c.Owner.GroupId)
                    .OrderByDescending(g => g.Max(c => c.Owner.UpdatedAt)).ToList();
                var selected = groups.Where((g, index) => index >= maxCount ||
                    ((now ?? DateTimeOffset.UtcNow) - g.Max(c => c.Owner.UpdatedAt)).TotalDays > maxDays).ToHashSet();
                var remaining = groups.Where(g => !selected.Contains(g)).Sum(g => g.Sum(c => c.Bytes));
                foreach (var group in groups.AsEnumerable().Reverse())
                {
                    if (remaining <= maxBytes) break;
                    if (selected.Add(group)) remaining -= group.Sum(c => c.Bytes);
                }
                foreach (var group in selected.OrderBy(g => g.Max(c => c.Owner.UpdatedAt)))
                {
                    try
                    {
                        foreach (var copy in group) RetainedDiagnostics.Save(copy.Root, copy.Owner, copy.UserRoots);
                        foreach (var copy in group)
                        {
                            foreach (var userRoot in copy.UserRoots) Directory.Delete(userRoot, true);
                            // Windows cannot remove a FileShare.None lease; the global pruning gate still owns this decision.
                            File.SetLastWriteTimeUtc(Path.Combine(copy.Root, ".owner.json"), DateTime.UtcNow);
                            copy.Lease.Dispose();
                            Directory.Delete(copy.Root, true);
                            deleted.Add(copy.Root);
                            RunnerConsole.WriteLine("Retention removed " + copy.Root + " (" + copy.Bytes + " bytes)");
                        }
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    { RunnerConsole.WriteLine("Retention preserved " + group.Key + ": " + error.Message); }
                }
                return deleted;
            }
            finally { foreach (var copy in copies) copy.Dispose(); }
        }

        private static bool IsLinked(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static long Measure(string path)
        {
            if (IsLinked(path)) throw new IOException("linked retained tree: " + path);
            if (!Directory.Exists(path)) return new FileInfo(path).Length;
            return Directory.EnumerateFileSystemEntries(path).Sum(Measure);
        }

        private static IEnumerable<string> DefaultHomes()
        {
            if (OperatingSystem.IsWindows()) yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            else if (OperatingSystem.IsMacOS()) yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
            else yield return Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
    }
}
