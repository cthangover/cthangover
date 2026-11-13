namespace Cthangover.Core.Mods.Compilation
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// Owns a cross-process compilation lease for one artifact. Complete DLL/PDB pairs
    /// are immutable: publish the PDB first and the DLL last, then only update a usage marker.
    /// Lock files are never unlinked, which would split locks between inodes on Unix.
    /// </summary>
    public sealed class AssemblyCacheEntry : IDisposable
    {
        private readonly FileStream lease;
        private readonly string path;

        private AssemblyCacheEntry(string path, FileStream lease)
        {
            this.path = path;
            this.lease = lease;
        }

        /// <summary>Acquires the artifact's exclusive file lease, honouring cancellation and a bounded wait.</summary>
        public static AssemblyCacheEntry Acquire(string path, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var watch = Stopwatch.StartNew();
            var limit = timeout ?? TimeSpan.FromMinutes(2);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return new AssemblyCacheEntry(path, new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                }
                catch (IOException)
                {
                    if (watch.Elapsed >= limit) throw new TimeoutException("Compilation cache lease timed out: " + path);
                    if (cancellationToken.WaitHandle.WaitOne(25)) cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        /// <summary>Whether both committed artifacts exist; callers hold the lease while checking and publishing.</summary>
        public bool HasArtifacts => File.Exists(path) && File.Exists(Path.ChangeExtension(path, ".pdb"));

        /// <summary>Removes only an interrupted, incomplete publication; complete pairs are never changed here.</summary>
        public void RemoveIncomplete()
        {
            if (HasArtifacts) return;
            File.Delete(path);
            File.Delete(Path.ChangeExtension(path, ".pdb"));
        }

        /// <summary>Publishes one complete pair, or reuses the immutable pair already published for this key.</summary>
        public void Publish(Stream assembly, Stream symbols)
        {
            if (HasArtifacts) { Touch(); return; }
            RemoveIncomplete();
            var temporary = path + "." + Guid.NewGuid().ToString("N");
            var temporaryPdb = temporary + ".pdb.tmp";
            var temporaryDll = temporary + ".dll.tmp";
            try
            {
                Write(temporaryDll, assembly);
                Write(temporaryPdb, symbols);
                File.Move(temporaryPdb, Path.ChangeExtension(path, ".pdb"), true);
                File.Move(temporaryDll, path);
                Touch();
            }
            finally
            {
                File.Delete(temporaryDll);
                File.Delete(temporaryPdb);
            }
        }

        /// <summary>Records a hit without changing artifact timestamps used by assembly reference selection.</summary>
        public void Touch() => File.WriteAllText(path + ".used", string.Empty);

        private static void Write(string destination, Stream source)
        {
            source.Position = 0;
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(output);
            output.Flush(true);
        }

        /// <summary>Releases the OS lease; its named file remains for the next process.</summary>
        public void Dispose() => lease.Dispose();
    }
}
