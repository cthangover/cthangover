namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Reuses one sealed project per source identity. Shared kernel leases prohibit upgrading its revision while
    /// any reader is active; exclusive incremental preparation never mixes changing live source bytes.
    /// </summary>
    public static class PreparedInputStore
    {
        private const string FixedUserDirectory = "[application]\nconfig/use_custom_user_dir=true\n"
            + "config/custom_user_dir_name=\"cthangover-tests/data\"\n";

        /// <summary>
        /// Acquires checked immutable inputs. A null build is for tiny preparation fixtures; production supplies a
        /// build request. Lazy source hashing trusts unchanged source metadata and keeps prepared byte verification.
        /// Observer phases are synchronous and caller-owned; cancellation never publishes a ready receipt.
        /// </summary>
        public static PreparedInputLease Acquire(string source, string cacheRoot, PreparedBuildRequest build = null,
            CancellationToken cancellationToken = default, IPreparedInputObserver observer = null, bool lazySourceHashes = false)
        {
            return AcquireCore(source, cacheRoot, build, cancellationToken, observer, false, lazySourceHashes);
        }

        /// <summary>
        /// Explicitly repairs a quarantined generation under the exclusive gate. Actual copied/deleted inputs and
        /// quarantine-repair reason remain visible; ordinary Acquire never performs this recovery implicitly.
        /// </summary>
        public static PreparedInputLease Repair(string source, string cacheRoot, PreparedBuildRequest build = null,
            CancellationToken cancellationToken = default, IPreparedInputObserver observer = null)
        {
            return AcquireCore(source, cacheRoot, build, cancellationToken, observer, true, false);
        }

        private static PreparedInputLease AcquireCore(string source, string cacheRoot, PreparedBuildRequest build,
            CancellationToken cancellationToken, IPreparedInputObserver observer, bool repair, bool lazySourceHashes)
        {
            var started = Stopwatch.GetTimestamp();
            var metrics = new InputPreparationMetrics();
            using var process = Process.GetCurrentProcess();
            var initialCpu = process.TotalProcessorTime.TotalSeconds;
            metrics.Sample();
            FileStream gate = null;
            try
            {
                source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
                cacheRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cacheRoot));
                metrics.CacheRoot = cacheRoot;
                if (WorkspaceOwner.IsWithin(cacheRoot, source) || WorkspaceOwner.IsWithin(source, cacheRoot))
                {
                    throw new PreparedInputRefusal("cache-inside-source", metrics);
                }

                PreparedInputFiles.RejectLink(source);
                PreparedInputFiles.RejectLink(cacheRoot);
                var identity = InputRetention.Identity(source);
                var store = Path.Combine(cacheRoot, "prepared-v1", identity);
                var root = Path.Combine(store, "project");
                metrics.PreparedRoot = root;
                metrics.ManifestPath = Path.Combine(store, "ready.json");
                Directory.CreateDirectory(store);
                var gatePath = Path.Combine(store, "revision.lock");
                EnsureGate(gatePath);
                RefuseRetiring(store, metrics);
                RefuseProcesses(root, metrics, mutation: repair);
                var toolchain = build == null ? null : PreparedBuildCache.Toolchain(build, cancellationToken);
                if (lazySourceHashes)
                {
                    gate = TryRead(gatePath);
                }

                var ready = gate == null ? null : PreparedInputJson.Read<PreparedInputManifest>(metrics.ManifestPath);
                var sourceHashes = new PreparedSourceHashes(lazySourceHashes ? Path.Combine(store, "source-hashes-v1.json") : null,
                    ready?.SourceFingerprint, metrics);
                var sourceFiles = PreparedInputFiles.Scan(source, sourceHashes, cancellationToken);
                metrics.Sample();
                var fingerprint = PreparedInputFiles.Fingerprint(sourceFiles, toolchain);
                metrics.SourceFingerprint = fingerprint;
                metrics.PreparedInputId = "inputs-v1-" + fingerprint;
                Notify(observer, "scanned", root, fingerprint, cancellationToken);
                if (gate == null)
                {
                    gate = TryRead(gatePath);
                    ready = gate == null ? null : PreparedInputJson.Read<PreparedInputManifest>(metrics.ManifestPath);
                }
                RefuseRetiring(store, metrics);
                RefuseProcesses(root, metrics, mutation: repair);
                var quarantined = File.Exists(Path.Combine(store, "quarantined.json"));
                if (quarantined)
                {
                    if (!repair)
                    {
                        throw new PreparedInputRefusal("prepared-input-mutated", metrics);
                    }

                    metrics.ConflictReasons.Add("quarantine-repair");
                }

                if (gate != null && ready?.SourceFingerprint == fingerprint && !quarantined)
                {
                    if (PreparedBuildCache.Validate(root, ready, build, toolchain, metrics))
                    {
                        var changed = Verify(root, ready);
                        if (changed.Count > 0)
                        {
                            PreparedInputJson.Write(Path.Combine(store, "quarantined.json"), changed);
                            throw new PreparedInputRefusal("prepared-input-mutated", metrics);
                        }

                        Notify(observer, "before-seal", root, fingerprint, cancellationToken);
                        Fence(source, sourceFiles, toolchain, sourceHashes, metrics, cancellationToken);
                        sourceHashes.Save(fingerprint);
                        metrics.CacheHit = true;
                        metrics.ReusedInputs = CountFiles(ready.Files);
                        metrics.PrepareSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
                        var lease = new PreparedInputLease(root, store, gate, ready, metrics);
                        gate = null;
                        return lease;
                    }
                }

                gate?.Dispose();
                gate = null;
                using (var writer = TryWrite(gatePath) ?? throw new PreparedInputRefusal("prepared-revision-busy", metrics))
                {
                    RefuseRetiring(store, metrics);
                    RefuseProcesses(root, metrics, mutation: true);
                    PreparedInputJson.Write(Path.Combine(store, "origin.json"), new PreparedInputOrigin
                    {
                        Source = source,
                        Identity = identity,
                        Root = root
                    });
                    ready = PreparedInputJson.Read<PreparedInputManifest>(metrics.ManifestPath);
                    if (build?.NoBuild == true && ready?.Build == null)
                    {
                        throw new PreparedInputRefusal("build-receipt-missing", metrics);
                    }

                    if (File.Exists(metrics.ManifestPath))
                    {
                        File.Delete(metrics.ManifestPath);
                    }

                    var initialPopulation = !Directory.Exists(root);
                    Directory.CreateDirectory(root);
                    var desired = Desired(source, sourceFiles, cancellationToken);
                    Synchronize(source, root, desired, metrics, cancellationToken);
                    metrics.Sample();
                    RemoveExtraneous(root, desired, ready?.Build, metrics);
                    metrics.FullProjectCopies = initialPopulation ? 1 : 0;
                    Notify(observer, "materialized", root, fingerprint, cancellationToken);
                    Fence(source, sourceFiles, toolchain, sourceHashes, metrics, cancellationToken);
                    var candidate = new PreparedInputManifest
                    {
                        Source = source,
                        SourceFingerprint = fingerprint,
                        Files = desired,
                        Build = ready?.Build
                    };
                    PreparedBuildCache.Ensure(root, candidate, build, toolchain, metrics, cancellationToken);
                    metrics.Sample();
                    Notify(observer, "before-seal", root, fingerprint, cancellationToken);
                    Fence(source, sourceFiles, toolchain, sourceHashes, metrics, cancellationToken);
                    foreach (var file in All(candidate))
                    {
                        if (!file.IsDirectory)
                        {
                            File.SetAttributes(PreparedInputFiles.SafePath(root, file.Path), FileAttributes.ReadOnly);
                        }
                    }

                    PreparedInputJson.Write(metrics.ManifestPath, candidate);
                    var quarantine = Path.Combine(store, "quarantined.json");
                    if (File.Exists(quarantine))
                    {
                        File.Delete(quarantine);
                    }
                }

                gate = TryRead(gatePath) ?? throw new PreparedInputRefusal("prepared-revision-busy", metrics);
                ready = PreparedInputJson.Read<PreparedInputManifest>(metrics.ManifestPath);
                if (ready?.SourceFingerprint != fingerprint)
                {
                    throw new PreparedInputRefusal("source-revision-changed", metrics);
                }

                sourceHashes.Save(fingerprint);
                metrics.PrepareSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
                process.Refresh();
                metrics.CpuSeconds = Math.Max(0, process.TotalProcessorTime.TotalSeconds - initialCpu);
                metrics.Sample();
                var acquired = new PreparedInputLease(root, store, gate, ready, metrics);
                gate = null;
                return acquired;
            }
            catch (OperationCanceledException)
            {
                metrics.ConflictReasons.Add("preparation-cancelled");
                throw;
            }
            catch (PreparedInputRefusal)
            {
                throw;
            }
            catch (Exception error)
            {
                throw new PreparedInputRefusal("preparation-failed", metrics, error);
            }
            finally
            {
                gate?.Dispose();
                metrics.PrepareSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
                process.Refresh();
                metrics.CpuSeconds = Math.Max(0, process.TotalProcessorTime.TotalSeconds - initialCpu);
                metrics.Sample();
            }
        }

        /// <summary>Prunes only self-marked inactive prepared stores; an OS-held reader or writer always protects its root.</summary>
        public static IReadOnlyList<InputRetentionDecision> Prune(string cacheRoot, int maxCount, double maxDays,
            long maxBytes, DateTimeOffset? now = null, RetentionWorkOptions work = null,
            CancellationToken cancellationToken = default, IInputRetentionObserver observer = null)
        {
            return InputRetention.Prune(cacheRoot, maxCount, maxDays, maxBytes, now, work, cancellationToken, observer);
        }

        private static void RefuseRetiring(string store, InputPreparationMetrics metrics)
        {
            if (File.Exists(Path.Combine(store, "retiring")))
            {
                throw new PreparedInputRefusal("prepared-input-retiring", metrics);
            }
        }

        private static void RefuseProcesses(string root, InputPreparationMetrics metrics, bool mutation)
        {
            foreach (var process in PendingProcessClaim.ReadAll(root))
            {
                if (mutation ? PendingProcessClaim.BlocksMutation(process) : PendingProcessClaim.BlocksRead(process))
                {
                    throw new PreparedInputRefusal("prepared-process-unconfirmed", metrics);
                }
            }
        }

        internal static List<string> Verify(string root, PreparedInputManifest manifest, bool preserveScratchDirectories = true)
        {
            var changes = new HashSet<string>(StringComparer.Ordinal);
            var expected = new HashSet<string>(StringComparer.Ordinal);
            var directories = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in All(manifest))
            {
                expected.Add(file.Path);
                if (!PreparedInputFiles.Matches(root, file))
                {
                    changes.Add(file.Path);
                }

                var parent = file.IsDirectory ? file.Path : Path.GetDirectoryName(file.Path)?.Replace('\\', '/');
                while (!string.IsNullOrEmpty(parent))
                {
                    directories.Add(parent);
                    parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                }
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!expected.Contains(relative) && !PreparedInputFiles.Scratch(relative))
                {
                    changes.Add(relative);
                }
            }

            PreparedInputDirectories.FindUnexpectedEmpty(root, root, directories, changes, preserveScratchDirectories);
            return changes.Order(StringComparer.Ordinal).ToList();
        }

        internal static IEnumerable<PreparedInputFile> All(PreparedInputManifest manifest)
        {
            foreach (var file in manifest.Files)
            {
                yield return file;
            }

            if (manifest.Build != null)
            {
                foreach (var file in manifest.Build.Outputs)
                {
                    yield return file;
                }
            }
        }

        internal static FileStream TryRead(string gate)
        {
            try
            {
                return new FileStream(gate, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                return null;
            }
        }

        internal static FileStream TryWrite(string gate)
        {
            try
            {
                return new FileStream(gate, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static void EnsureGate(string path)
        {
            if (!File.Exists(path))
            {
                try
                {
                    using var created = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Another owner created the empty coordination file first.
                }
            }
        }

        private static List<PreparedInputFile> Desired(string source, List<PreparedInputFile> files, CancellationToken token)
        {
            var result = new List<PreparedInputFile>();
            foreach (var file in files)
            {
                if (file.Path != "override.cfg")
                {
                    result.Add(file);
                }
            }

            var bytes = Override(source);
            result.Add(new PreparedInputFile
            {
                Path = "override.cfg",
                Length = bytes.LongLength,
                Hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
            });
            token.ThrowIfCancellationRequested();
            return result;
        }

        private static byte[] Override(string source)
        {
            var file = Path.Combine(source, "override.cfg");
            var original = File.Exists(file) ? File.ReadAllText(file) : "";
            return new UTF8Encoding(false).GetBytes(original + "\n" + FixedUserDirectory);
        }

        private static void Synchronize(string source, string root, List<PreparedInputFile> files,
            InputPreparationMetrics metrics, CancellationToken token)
        {
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                var target = PreparedInputFiles.SafePath(root, file.Path);
                if (file.IsDirectory)
                {
                    PreparedInputFiles.RejectLink(target);
                    Directory.CreateDirectory(target);
                    continue;
                }

                if (PreparedInputFiles.Matches(root, file))
                {
                    metrics.ReusedInputs++;
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                PreparedInputFiles.RejectLink(target);
                PreparedInputFiles.Writable(target);
                if (file.Path == "override.cfg")
                {
                    File.WriteAllBytes(target, Override(source));
                }
                else
                {
                    File.Copy(PreparedInputFiles.SafePath(source, file.Path), target, true);
                }

                metrics.FilesCopied++;
                metrics.BytesCopied += new FileInfo(target).Length;
                if (!PreparedInputFiles.Matches(root, file))
                {
                    throw new PreparedInputRefusal("source-revision-changed", metrics);
                }
            }
        }

        private static void Fence(string source, List<PreparedInputFile> expected, string toolchain,
            PreparedSourceHashes sourceHashes, InputPreparationMetrics metrics, CancellationToken token)
        {
            if (PreparedInputFiles.Fingerprint(PreparedInputFiles.Scan(source, sourceHashes, token), toolchain)
                != PreparedInputFiles.Fingerprint(expected, toolchain))
            {
                throw new PreparedInputRefusal("source-revision-changed", metrics);
            }
        }

        private static void RemoveExtraneous(string root, List<PreparedInputFile> files, PreparedBuildReceipt build,
            InputPreparationMetrics metrics)
        {
            var retained = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                retained.Add(file.Path);
            }

            if (build != null)
            {
                foreach (var file in build.Outputs)
                {
                    retained.Add(file.Path);
                }
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!retained.Contains(relative) && !PreparedInputFiles.Scratch(relative))
                {
                    PreparedInputFiles.RejectLink(file);
                    PreparedInputFiles.Writable(file);
                    File.Delete(file);
                    metrics.FilesRemoved++;
                }
            }

            var desired = new List<PreparedInputFile>(files);
            if (build != null)
            {
                desired.AddRange(build.Outputs);
            }
            PreparedInputDirectories.RemoveUnexpectedEmpty(root, root,
                PreparedInputDirectories.Expected(desired), preserveScratch: true);
        }

        private static void Notify(IPreparedInputObserver observer, string phase, string root, string fingerprint,
            CancellationToken token)
        {
            observer?.Observe(phase, root, fingerprint);
            token.ThrowIfCancellationRequested();
        }

        private static long CountFiles(IEnumerable<PreparedInputFile> files)
        {
            long count = 0;
            foreach (var file in files)
            {
                if (!file.IsDirectory)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
