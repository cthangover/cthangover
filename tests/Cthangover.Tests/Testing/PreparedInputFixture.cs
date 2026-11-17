namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Threading;
    using Cthangover.TestRunner;

    /// <summary>Exercises physical preparation on a tiny owned source, never on the game, SDK or another test's cache.</summary>
    internal sealed class PreparedInputFixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "cthangover-prepared-" + Guid.NewGuid().ToString("N"));

        internal string Source => Path.Combine(Root, "source");

        internal string Cache => Path.Combine(Root, "cache");

        internal string Payload => Path.Combine("mods", "probe", "data.json");

        internal PreparedBuildRequest Build { get; set; }

        internal CancellationToken Token { get; set; }

        internal IPreparedInputObserver Observer { get; set; }

        internal PreparedInputFixture()
        {
            Directory.CreateDirectory(Path.Combine(Source, "mods", "probe"));
            File.WriteAllText(Path.Combine(Source, "project.godot"), "config_version=5\n");
            File.WriteAllText(Path.Combine(Source, "mods", "probe", "manifest.json"), "{\"id\":\"probe\"}");
            File.WriteAllText(Path.Combine(Source, Payload), "{\"value\":1}");
        }

        internal PreparedInputLease Acquire()
        {
            return PreparedInputStore.Acquire(Source, Cache, Build, Token, Observer);
        }

        internal void AcquireAndRelease()
        {
            using var lease = Acquire();
        }

        internal void ChangeSource()
        {
            var path = Path.Combine(Source, Payload);
            var timestamp = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path, "{\"value\":2}");
            File.SetLastWriteTimeUtc(path, timestamp);
        }

        /// <summary>Removes only this fixture's source and cache after every caller has released its leases.</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                foreach (var path in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                }

                Directory.Delete(Root, true);
            }
        }
    }
}
