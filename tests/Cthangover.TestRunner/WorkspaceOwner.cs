namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Text.Json;

    /// <summary>Owns a workspace from before its copy starts through cleanup; the open lease also protects it from the container cleaner.</summary>
    public sealed class WorkspaceOwner : IDisposable
    {
        private readonly string root;
        private readonly FileStream lease;
        private readonly WorkspaceOwnerInfo info;
        private bool disposed;

        /// <summary>Publishes the report destination before any copying, so an interrupted preparation remains diagnosable.</summary>
        public WorkspaceOwner(string root, string runId, string groupId, string artifactsDirectory)
        {
            this.root = root;
            Directory.CreateDirectory(root);
            lease = new FileStream(Path.Combine(root, ".owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var process = Process.GetCurrentProcess();
            info = new WorkspaceOwnerInfo { RunId = runId, GroupId = groupId, ProcessId = process.Id,
                ProcessStartedTicks = process.StartTime.ToUniversalTime().Ticks, Active = true,
                ArtifactsDirectory = artifactsDirectory };
            try { Write(); }
            catch { lease.Dispose(); throw; }
        }

        /// <summary>Records only data directories outside the physical run; shared homes already belong to the run root.</summary>
        public void TrackUserDirectory(string path)
        {
            if (IsWithin(path, root)) return;
            lock (info)
            {
                if (!info.UserDirectories.Contains(path)) info.UserDirectories.Add(path);
                Write();
            }
        }

        private void Write()
        {
            info.UpdatedAt = DateTimeOffset.UtcNow;
            var path = Path.Combine(root, ".owner.json");
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(info));
            File.Move(temporary, path, true);
        }

        /// <summary>Checks both PID and start time; a dead or reused PID is not an owner.</summary>
        public static bool IsAlive(WorkspaceOwnerInfo info)
        {
            if (!info.Active) return false;
            try
            {
                using var process = Process.GetProcessById(info.ProcessId);
                return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == info.ProcessStartedTicks;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return true; } // Cannot prove it is dead.
        }

        internal static bool IsWithin(string path, string root)
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
            return relative == "." || (!Path.IsPathRooted(relative) && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }

        internal void ReleaseLease() => lease.Dispose();

        /// <summary>Marks retained/interrupted copies inactive in finally; successful deletion may already have removed the marker.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (Directory.Exists(root))
                {
                    lock (info) { info.Active = false; Write(); }
                }
            }
            finally { lease.Dispose(); }
        }
    }
}
