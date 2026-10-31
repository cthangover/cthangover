namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Cthangover.Core.Mods.Pipeline;
    using Cthangover.Core.Mods.Providers;

    /// <summary>
    /// Preserves failed package identities for the manager and validates passwords outside Godot.
    /// Successful unlocks retain credentials for the session; persistence is an explicit opt-in.
    /// </summary>
    public sealed class ArchiveUnlockService
    {
        private readonly object gate = new();
        private readonly Dictionary<string, UnavailableModArchive> unavailable = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly ArchivePasswordStore passwords;

        /// <summary>Process service used by discovery and the manager.</summary>
        public static ArchiveUnlockService Default { get; } = new ArchiveUnlockService();

        /// <summary>Uses a supplied credential store for isolated hosts/tests, or the boot-owned user store.</summary>
        public ArchiveUnlockService(ArchivePasswordStore passwords = null) => this.passwords = passwords;

        /// <summary>Returns an independent snapshot, omitting packages removed from disk.</summary>
        public IReadOnlyList<UnavailableModArchive> GetUnavailable()
        {
            lock (gate)
            {
                var result = new List<UnavailableModArchive>();
                foreach (var item in unavailable.Values)
                    if (File.Exists(item.Path))
                        result.Add(item);
                return result;
            }
        }

        /// <summary>Updates the visible failure for a local package without exposing diagnostic secrets.</summary>
        public void RecordFailure(string path, bool needsPassword)
        {
            var item = new UnavailableModArchive(path, needsPassword);
            lock (gate)
                unavailable[item.Path] = item;
        }

        /// <summary>Removes a failure after successful discovery/unlock or explicit removal.</summary>
        public void Remove(string path)
        {
            lock (gate)
                unavailable.Remove(Path.GetFullPath(path));
        }

        /// <summary>
        /// Validates manifest and encrypted content before accepting a credential. Does not load code;
        /// the caller subsequently prepares/applies the current mod selection through the normal reload pipeline.
        /// </summary>
        public string Unlock(string path, string password, bool remember = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var provider = OpenProvider(path, password);
            var manifest = ModDiscoveryService.ReadManifest(provider, out var problem);
            if (manifest == null)
                throw new InvalidDataException(problem);
            provider.ValidatePassword(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var store = passwords ?? ArchivePasswordStore.Default;
            if (remember)
                store.Set(path, password);
            else
                store.SetSession(path, password);
            Remove(path);
            return manifest.Id;
        }

        /// <summary>Opens a local ZIP/7z provider; its contents are detected by the shared managed backend.</summary>
        public static ArchiveModFileProvider OpenProvider(string path, string password = null)
        {
            if (path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                return new SevenZipModFileProvider(path, Path.GetFileNameWithoutExtension(path), password);
            return new ZipModFileProvider(path, Path.GetFileNameWithoutExtension(path), password);
        }
    }
}
