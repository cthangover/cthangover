namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    /// <summary>Bounded owned cleanup preserves coordination through interruption and never admits a retiring generation.</summary>
    internal static class InputRetention
    {
        internal static IReadOnlyList<InputRetentionDecision> Prune(string cacheRoot, int maxCount, double maxDays,
            long maxBytes, DateTimeOffset? now, RetentionWorkOptions work, CancellationToken cancellationToken,
            IInputRetentionObserver observer)
        {
            if (maxCount < 0 || maxDays < 0 || !double.IsFinite(maxDays) || maxBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }
            var budget = new InputRetentionBudget(work, cancellationToken, observer);
            var output = new List<InputRetentionDecision>();
            var root = Path.Combine(Path.GetFullPath(cacheRoot), "prepared-v1");
            var candidates = new List<(string Path, DateTimeOffset LastUsed)>();
            try
            {
                if (!budget.GuardPath(root) || !budget.Take(null))
                {
                    return DiscoveryDeferred(root, now, budget);
                }
                using var entries = new DirectoryInfo(root).EnumerateDirectories().GetEnumerator();
                while (true)
                {
                    if (!budget.Take(null))
                    {
                        return DiscoveryDeferred(root, now, budget);
                    }
                    if (!entries.MoveNext())
                    {
                        break;
                    }
                    budget.EntryVisited();
                    var entry = entries.Current;
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException("Linked retention candidate refused: " + entry.FullName);
                    }
                    if (!LastUse(entry.FullName, budget, out var lastUsed))
                    {
                        return DiscoveryDeferred(root, now, budget);
                    }
                    candidates.Add((entry.FullName, lastUsed));
                }
            }
            catch (DirectoryNotFoundException)
            {
                return output;
            }
            catch (FileNotFoundException)
            {
                return output;
            }
            candidates.Sort(RecentFirst);
            long retained = 0;
            var index = 0;
            foreach (var candidate in candidates)
            {
                var store = candidate.Path;
                var started = Stopwatch.GetTimestamp();
                var decision = NewDecision(store, now);
                output.Add(decision);
                budget.Attach(decision);
                try
                {
                    var tree = new InputRetentionTree(store, budget, decision);
                    if (!tree.GuardDirectory(store) || !budget.Take(decision))
                    {
                        Defer(decision, store, budget, false);
                        break;
                    }
                    decision.AttributeReads++;
                    var marker = Path.Combine(store, "retiring");
                    var retiring = File.Exists(marker);
                    decision.Partial = retiring;
                    var originPath = Path.Combine(store, "origin.json");
                    if (!tree.GuardEntry(originPath) || !budget.Take(decision))
                    {
                        Defer(decision, store, budget, false);
                        break;
                    }
                    decision.ControlRecordsRead++;
                    var origin = JsonSerializer.Deserialize<PreparedInputOrigin>(File.ReadAllText(originPath), ReportWriter.JsonOptions);
                    if (origin == null || origin.SchemaVersion != 1 || origin.Identity != decision.Identity
                        || origin.Identity != Identity(origin.Source)
                        || Path.GetFullPath(origin.Root) != Path.Combine(Path.GetFullPath(store), "project"))
                    {
                        decision.Decision = "refused-ownership";
                        decision.Reason = "unrecognized-store-owner";
                        continue;
                    }
                    if (!budget.Take(decision))
                    {
                        Defer(decision, store, budget, false);
                        break;
                    }
                    using var gate = PreparedInputStore.TryWrite(Path.Combine(store, "revision.lock"));
                    if (gate == null)
                    {
                        decision.Decision = "retained-active";
                        decision.Reason = "active-input-or-lane-lease";
                        continue;
                    }
                    if (!PruneInactiveLeases(store, origin.Root, budget, decision, tree))
                    {
                        Defer(decision, store, budget, retiring);
                        break;
                    }
                    if (decision.LeaseIds.Length > 0)
                    {
                        decision.Decision = "retained-active";
                        decision.Reason = "active-input-or-lane-lease";
                        continue;
                    }
                    var processes = PendingProcessClaim.ReadAll(origin.Root, budget, decision);
                    var processBlocked = false;
                    foreach (var process in processes)
                    {
                        processBlocked |= PendingProcessClaim.BlocksMutation(process);
                    }
                    if (processBlocked)
                    {
                        decision.Decision = "retained-active";
                        decision.Reason = "unconfirmed-process-owner";
                        continue;
                    }
                    if (CoordinationOnly(store, budget, decision))
                    {
                        decision.Decision = "removed";
                        decision.Reason = "coordination-only";
                        continue;
                    }
                    var expired = retiring || index >= maxCount
                        || (decision.At - candidate.LastUsed).TotalDays > maxDays || maxBytes == 0;
                    index++;
                    if (!expired)
                    {
                        if (!tree.Measure(store, out var bytes))
                        {
                            Defer(decision, store, budget, false);
                            decision.Reason = "measurement-budget";
                            break;
                        }
                        decision.Bytes = bytes;
                        expired = retained + bytes > maxBytes;
                        if (!expired)
                        {
                            retained += bytes;
                            decision.Decision = "retained-recent";
                            decision.Reason = "within-count-age-byte-limits";
                            continue;
                        }
                    }
                    if (!retiring)
                    {
                        if (!budget.Take(decision))
                        {
                            Defer(decision, store, budget, false);
                            break;
                        }
                        File.WriteAllText(marker, decision.At.ToString("O"));
                        decision.Partial = true;
                        budget.Observe("retiring", store, decision);
                    }
                    decision.Partial = true;
                    if (!tree.Delete(store, root: true) || !budget.Take(decision))
                    {
                        Defer(decision, store, budget, true);
                        break;
                    }
                    // Origin and the locked coordination file remain recognizable. Clear retiring only
                    // after payload/ready metadata are gone; a subsequent admission must prepare anew.
                    File.Delete(marker);
                    decision.Partial = false;
                    decision.Bytes = Math.Max(decision.Bytes, decision.BytesRemoved);
                    decision.Decision = "removed";
                    decision.Reason = "inactive-retention-limit";
                    budget.Observe("completed", store, decision);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                {
                    if (error.Message is "process-owner-budget" or "coordination-budget")
                    {
                        Defer(decision, store, budget, decision.Partial);
                    }
                    else
                    {
                        decision.Decision = "refused-ownership";
                        decision.Reason = error.Message;
                    }
                }
                finally
                {
                    decision.ElapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
                }
            }
            return output;
        }

        internal static string Identity(string source)
        {
            source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                OperatingSystem.IsWindows() ? source.ToUpperInvariant() : source))).ToLowerInvariant();
        }

        private static InputRetentionDecision NewDecision(string store, DateTimeOffset? now)
        {
            return new InputRetentionDecision
            {
                Kind = "prepared-input",
                Path = store,
                Identity = Path.GetFileName(store),
                At = now ?? DateTimeOffset.UtcNow
            };
        }

        private static void Defer(InputRetentionDecision decision, string store, InputRetentionBudget budget, bool partial)
        {
            decision.Decision = "deferred";
            decision.Reason = budget.Reason;
            decision.Partial |= partial;
            budget.Observe("deferred", store, decision);
        }

        private static IReadOnlyList<InputRetentionDecision> DiscoveryDeferred(string root, DateTimeOffset? now,
            InputRetentionBudget budget)
        {
            var decision = NewDecision(root, now);
            budget.Attach(decision);
            decision.ElapsedSeconds = budget.ElapsedSeconds;
            Defer(decision, root, budget, false);
            return new[] { decision };
        }

        private static bool CoordinationOnly(string store, InputRetentionBudget budget, InputRetentionDecision decision)
        {
            if (!budget.Take(decision))
            {
                throw new IOException("coordination-budget");
            }
            using var entries = Directory.EnumerateFileSystemEntries(store).GetEnumerator();
            while (true)
            {
                if (!budget.Take(decision))
                {
                    throw new IOException("coordination-budget");
                }
                if (!entries.MoveNext())
                {
                    return true;
                }
                decision.EntriesVisited++;
                if (Path.GetFileName(entries.Current) is not ("origin.json" or "revision.lock"))
                {
                    return false;
                }
            }
        }

        private static bool PruneInactiveLeases(string store, string preparedRoot, InputRetentionBudget budget,
            InputRetentionDecision decision, InputRetentionTree tree)
        {
            var folder = Path.Combine(store, "leases");
            if (!budget.Take(decision))
            {
                return false;
            }
            decision.AttributeReads++;
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(folder);
            }
            catch (FileNotFoundException)
            {
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return true;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Linked lease directory refused: " + folder);
            }
            if (!tree.GuardDirectory(folder) || !budget.Take(decision))
            {
                return false;
            }
            using var entries = new DirectoryInfo(folder).EnumerateFileSystemInfos().GetEnumerator();
            while (budget.Take(decision))
            {
                if (!entries.MoveNext())
                {
                    return true;
                }
                decision.EntriesVisited++;
                var entry = entries.Current;
                if (entry is not FileInfo file || entry.Extension != ".json")
                {
                    throw new IOException("Unknown lease registry entry: " + entry.FullName);
                }
                if (!tree.GuardEntry(entry.FullName))
                {
                    return false;
                }
                if (!budget.Take(decision))
                {
                    return false;
                }
                decision.ControlRecordsRead++;
                var lease = JsonSerializer.Deserialize<PreparedInputOwner>(File.ReadAllText(file.FullName), ReportWriter.JsonOptions);
                if (lease == null || lease.SchemaVersion != 1 || lease.LeaseId != Path.GetFileNameWithoutExtension(file.Name)
                    || !SamePath(lease.Root, preparedRoot) || lease.ProcessId <= 0 || lease.ProcessStartedTicks <= 0)
                {
                    throw new IOException("Unknown prepared lease owner: " + file.FullName);
                }
                var owner = new WorkspaceOwnerInfo
                {
                    Active = lease.Active,
                    ProcessId = lease.ProcessId,
                    ProcessStartedTicks = lease.ProcessStartedTicks
                };
                if (WorkspaceOwner.IsAlive(owner))
                {
                    decision.LeaseIds = new[] { lease.LeaseId };
                    return true;
                }
                if (!tree.DeleteFile(file, control: true))
                {
                    return false;
                }
            }
            return false;
        }

        private static bool SamePath(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return false;
            }
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static bool LastUse(string store, InputRetentionBudget budget, out DateTimeOffset lastUsed)
        {
            lastUsed = default;
            if (!budget.Take(null))
            {
                return false;
            }
            var date = File.GetLastWriteTimeUtc(Path.Combine(store, "last-used.json"));
            if (date <= DateTime.UnixEpoch)
            {
                if (!budget.Take(null))
                {
                    return false;
                }
                date = Directory.GetLastWriteTimeUtc(store);
            }
            lastUsed = date;
            return true;
        }

        private static int RecentFirst((string Path, DateTimeOffset LastUsed) left,
            (string Path, DateTimeOffset LastUsed) right)
        {
            return right.LastUsed.CompareTo(left.LastUsed);
        }
    }
}
