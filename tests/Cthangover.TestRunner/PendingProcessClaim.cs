namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Text.Json;

    /// <summary>
    /// Publishes parent ownership before a child can start. Only an observed safe terminal removes this claim;
    /// unsafe terminals remain durable even after the root PID exits because descendants may still hold inputs.
    /// </summary>
    internal sealed class PendingProcessClaim
    {
        private readonly object gate = new();
        private readonly string preparedRoot;
        private readonly string path;
        private readonly PreparedInputOwner owner;
        private bool completed;

        internal PendingProcessClaim(string preparedRoot, string projectRoot, string launchId, string runId,
            string preparedInputId)
        {
            this.preparedRoot = Canonical(preparedRoot);
            projectRoot = Canonical(projectRoot);
            ValidateRoot(this.preparedRoot, projectRoot);
            ValidateToken(launchId);
            ValidateToken(runId);
            ValidateToken(preparedInputId);
            var work = 0;
            CheckPath(this.preparedRoot, null, null, ref work);
            CheckPath(projectRoot, null, null, ref work);
            var directory = DirectoryPath(this.preparedRoot);
            CheckPath(directory, null, null, ref work);
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, launchId + ".json");
            CheckPath(path, null, null, ref work);
            using var parent = Process.GetCurrentProcess();
            owner = new PreparedInputOwner
            {
                LeaseId = launchId,
                RunId = runId,
                PreparedInputId = preparedInputId,
                Root = projectRoot,
                Access = "pending-process",
                ProcessId = parent.Id,
                ProcessStartedTicks = parent.StartTime.ToUniversalTime().Ticks,
                Active = true,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            Publish(owner, false);
        }

        internal void OnCompleted(ProcessResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            lock (gate)
            {
                if (completed)
                {
                    return;
                }

                var work = 0;
                CheckPath(path, null, null, ref work);
                var current = Read(path, preparedRoot, null, null, ref work)
                    ?? throw new IOException("Pending process claim disappeared before terminal publication");
                if (current.LeaseId != owner.LeaseId || current.RunId != owner.RunId
                    || current.PreparedInputId != owner.PreparedInputId || !SamePath(current.Root, owner.Root)
                    || current.Access != "pending-process" || current.ProcessId != owner.ProcessId
                    || current.ProcessStartedTicks != owner.ProcessStartedTicks)
                {
                    throw new IOException("Pending process claim identity changed before terminal publication");
                }

                if (result.CleanupSafe)
                {
                    File.Delete(path);
                }
                else
                {
                    owner.Access = result.TerminationState;
                    owner.ProcessId = result.ProcessId ?? 0;
                    owner.ProcessStartedTicks = result.ProcessCreatedAt?.UtcDateTime.Ticks ?? 0;
                    owner.UpdatedAt = DateTimeOffset.UtcNow;
                    Publish(owner, true);
                }

                completed = true;
            }
        }

        internal static IReadOnlyList<PreparedInputOwner> ReadAll(string preparedRoot, InputRetentionBudget budget = null,
            InputRetentionDecision decision = null)
        {
            preparedRoot = Canonical(preparedRoot);
            var directory = DirectoryPath(preparedRoot);
            var work = 0;
            CheckPath(preparedRoot, budget, decision, ref work);
            var output = new List<PreparedInputOwner>();
            if (!CheckPath(directory, budget, decision, ref work))
            {
                return output.AsReadOnly();
            }

            Take(budget, decision, ref work);
            var entries = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).GetEnumerator();
            using (entries)
            {
                while (true)
                {
                    Take(budget, decision, ref work);
                    if (!entries.MoveNext())
                    {
                        break;
                    }

                    if (decision != null)
                    {
                        decision.EntriesVisited++;
                    }

                    var path = entries.Current;
                    CheckPath(path, budget, decision, ref work);
                    var owner = Read(path, preparedRoot, budget, decision, ref work);
                    if (owner != null)
                    {
                        output.Add(owner);
                    }
                }
            }
            return output.AsReadOnly();
        }

        internal static bool BlocksRead(PreparedInputOwner owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (!owner.Active || owner.Access != "pending-process" || owner.ProcessId <= 0 || owner.ProcessStartedTicks <= 0)
            {
                return true;
            }

            try
            {
                using var parent = Process.GetProcessById(owner.ProcessId);
                return parent.HasExited || parent.StartTime.ToUniversalTime().Ticks != owner.ProcessStartedTicks;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException
                or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                return true;
            }
        }

        internal static bool BlocksMutation(PreparedInputOwner owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return owner.Active;
        }

        internal static bool BlocksLane(PreparedInputOwner owner, string laneRoot)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return owner.Active && SamePath(owner.Root, laneRoot);
        }

        private void Publish(PreparedInputOwner record, bool replace)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    JsonSerializer.Serialize(stream, record, ReportWriter.JsonOptions);
                    stream.Flush(true);
                }

                File.Move(temporary, path, replace);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static PreparedInputOwner Read(string path, string preparedRoot, InputRetentionBudget budget,
            InputRetentionDecision decision, ref int work)
        {
            try
            {
                Take(budget, decision, ref work);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (decision != null)
                {
                    decision.ControlRecordsRead++;
                }

                var owner = JsonSerializer.Deserialize<PreparedInputOwner>(stream, ReportWriter.JsonOptions);
                if (owner == null || owner.SchemaVersion != 1 || !owner.Active
                    || owner.Access is not "pending-process" and not "unproven-exit" and not "unfinished-pipes"
                    || owner.ProcessId < 0 || owner.ProcessStartedTicks < 0)
                {
                    throw new InvalidDataException("Invalid durable process owner record");
                }

                ValidateToken(owner.LeaseId);
                ValidateToken(owner.RunId);
                ValidateToken(owner.PreparedInputId);
                if (!string.Equals(Path.GetFileName(path), owner.LeaseId + ".json", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Process owner filename disagrees with launch identity");
                }

                ValidateRoot(preparedRoot, Canonical(owner.Root));
                CheckPath(owner.Root, budget, decision, ref work);
                return owner;
            }
            catch (FileNotFoundException)
            {
                // A safe terminal can atomically remove its own record while another reader enumerates claims.
                return null;
            }
            catch (JsonException error)
            {
                throw new InvalidDataException("Malformed durable process owner record", error);
            }
        }

        private static string DirectoryPath(string preparedRoot)
        {
            var store = Path.GetDirectoryName(preparedRoot)
                ?? throw new IOException("Prepared process root has no metadata parent");
            return Path.Combine(store, "process-owners");
        }

        private static void ValidateRoot(string preparedRoot, string projectRoot)
        {
            if (SamePath(preparedRoot, projectRoot))
            {
                return;
            }

            var pool = Path.Combine(Path.GetDirectoryName(preparedRoot), "write-lanes");
            for (var index = 0; index < 8; index++)
            {
                if (SamePath(projectRoot, Path.Combine(pool, "lane_" + index, "project")))
                {
                    return;
                }
            }

            throw new InvalidDataException("Process project root is neither the prepared root nor its bounded physical write lane");
        }

        private static void ValidateToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value is "." or "..")
            {
                throw new InvalidDataException("Invalid durable process identity");
            }

            foreach (var character in value)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')
                {
                    throw new InvalidDataException("Invalid durable process identity character");
                }
            }
        }

        private static string Canonical(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw new InvalidDataException("Process ownership roots must be absolute");
            }

            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static bool SamePath(string first, string second)
        {
            return string.Equals(Canonical(first), Canonical(second), OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static bool CheckPath(string path, InputRetentionBudget budget, InputRetentionDecision decision, ref int work)
        {
            var first = true;
            var exists = true;
            for (var current = Canonical(path); current != null; current = Path.GetDirectoryName(current))
            {
                Take(budget, decision, ref work);
                if (decision != null)
                {
                    decision.AttributeReads++;
                }

                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException("Linked process ownership path refused");
                    }
                }
                catch (FileNotFoundException)
                {
                    if (first)
                    {
                        exists = false;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    if (first)
                    {
                        exists = false;
                    }
                }

                first = false;
            }

            return exists;
        }

        private static void Take(InputRetentionBudget budget, InputRetentionDecision decision, ref int work)
        {
            if (budget == null ? ++work > 4096 : !budget.Take(decision))
            {
                throw new IOException("process-owner-budget");
            }
        }
    }
}
