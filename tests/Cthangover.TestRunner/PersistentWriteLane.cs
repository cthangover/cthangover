namespace Cthangover.TestRunner
{
    using System.Diagnostics;

    /// <summary>
    /// Bounded exclusive writable project with independent physical files. Restoration reads only the leased
    /// immutable baseline and transfers only changed/missing inputs; it never hardlinks to source or baseline.
    /// </summary>
    public sealed class PersistentWriteLane : IDisposable
    {
        private readonly PreparedInputLease inputs;
        private readonly FileStream gate;
        private readonly string store;
        private bool disposed;

        private PersistentWriteLane(PreparedInputLease inputs, string store, FileStream gate)
        {
            this.inputs = inputs;
            this.store = store;
            this.gate = gate;
            Root = Path.Combine(store, "project");
            Metrics = new WorkspacePreparationMetrics
            {
                LeaseId = Guid.NewGuid().ToString("N"),
                WorkspaceMode = "persistent-write",
                Root = Root,
                PreparedInputId = inputs.PreparedInputId
            };
        }

        /// <summary>Actual writable project; all ordinary files have independent storage from immutable input files.</summary>
        public string Root
        {
            get;
        }

        /// <summary>Measured lane creation/restoration work and its active owner identity.</summary>
        public WorkspacePreparationMetrics Metrics
        {
            get;
        }

        /// <summary>Claims one of a finite number of slots; busy pools refuse instead of creating unbounded copies.</summary>
        public static PersistentWriteLane Acquire(PreparedInputLease inputs, int slots = 2)
        {
            if (slots < 1 || slots > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(slots), "Write lane slots must be in 1..8");
            }

            var pool = Path.Combine(Path.GetDirectoryName(inputs.Root), "write-lanes");
            for (var index = 0; index < slots; index++)
            {
                var store = Path.Combine(pool, "lane_" + index);
                PreparedInputFiles.RejectLink(store);
                Directory.CreateDirectory(store);
                var occupied = false;
                foreach (var process in PendingProcessClaim.ReadAll(inputs.Root))
                {
                    if (PendingProcessClaim.BlocksLane(process, Path.Combine(store, "project")))
                    {
                        if (PendingProcessClaim.BlocksRead(process))
                        {
                            throw new PreparedInputRefusal("write-lane-process-unconfirmed", inputs.Metrics);
                        }
                        occupied = true;
                        break;
                    }
                }
                if (occupied)
                {
                    continue;
                }
                FileStream gate;
                try
                {
                    gate = new FileStream(Path.Combine(store, "lane.lock"), FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    continue;
                }

                var lane = new PersistentWriteLane(inputs, store, gate);
                try
                {
                    var initialPopulation = !Directory.Exists(lane.Root);
                    lane.Restore(false);
                    lane.Metrics.FullProjectCopies = initialPopulation ? 1 : 0;
                    return lane;
                }
                catch
                {
                    lane.Dispose();
                    throw;
                }
            }

            throw new PreparedInputRefusal("write-lane-busy", inputs.Metrics);
        }

        /// <summary>Repairs only differing bytes and removes additions; dependency chains call it only between components.</summary>
        public InputTransferCount Restore(bool componentBoundary = true)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var started = Stopwatch.GetTimestamp();
            var transferred = new InputTransferCount();
            var expected = new HashSet<string>(StringComparer.Ordinal);
            Directory.CreateDirectory(Root);
            foreach (var file in PreparedInputStore.All(inputs.Manifest))
            {
                expected.Add(file.Path);
                var target = PreparedInputFiles.SafePath(Root, file.Path);
                if (file.IsDirectory)
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                if (PreparedInputFiles.Matches(Root, file))
                {
                    Metrics.ReusedInputs++;
                    PreparedInputFiles.Writable(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                PreparedInputFiles.RejectLink(target);
                PreparedInputFiles.Writable(target);
                File.Copy(PreparedInputFiles.SafePath(inputs.Root, file.Path), target, true);
                PreparedInputFiles.Writable(target);
                transferred.Files++;
                transferred.Bytes += new FileInfo(target).Length;
                if (!PreparedInputFiles.Matches(Root, file))
                {
                    throw new IOException("Writable lane transfer differs from its sealed input: " + file.Path);
                }
            }

            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Root, file).Replace('\\', '/');
                if (!expected.Contains(relative))
                {
                    PreparedInputFiles.RejectLink(file);
                    PreparedInputFiles.Writable(file);
                    File.Delete(file);
                    Metrics.FilesRemoved++;
                }
            }

            PreparedInputDirectories.RemoveUnexpectedEmpty(Root, Root,
                PreparedInputDirectories.Expected(PreparedInputStore.All(inputs.Manifest)), preserveScratch: false);

            Metrics.FilesCopied += transferred.Files;
            Metrics.BytesCopied += transferred.Bytes;
            var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
            if (componentBoundary)
            {
                Metrics.RestoreFiles += transferred.Files;
                Metrics.RestoreBytes += transferred.Bytes;
                Metrics.RestoreSeconds += seconds;
            }
            else
            {
                Metrics.PrepareSeconds += seconds;
            }

            PreparedInputJson.Write(Path.Combine(store, "lane.json"), Metrics);
            return transferred;
        }

        /// <summary>Releases the pool slot without deleting its reusable physical input files.</summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                gate.Dispose();
            }
        }
    }
}
