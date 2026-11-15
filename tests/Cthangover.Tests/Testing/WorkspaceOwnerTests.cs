namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Cthangover.TestRunner;

    public sealed class WorkspaceOwnerTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "owner_contract_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void LeaseAndDurableMarkerCoverLifetimeAndExternalUserData()
        {
            var marker = Path.Combine(root, ".owner.json");
            using (var owner = new WorkspaceOwner(root, "run", "run", "/reports"))
            {
                owner.TrackUserDirectory(Path.Combine(root, "homes", "local"));
                owner.TrackUserDirectory(Path.Combine(Path.GetTempPath(), "external", "data"));
                var info = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(marker));
                Assert.True(info.Active);
                Assert.True(WorkspaceOwner.IsAlive(info));
                Assert.Equal("/reports", info.ArtifactsDirectory);
                Assert.Single(info.UserDirectories);
                Assert.Throws<IOException>(() => new FileStream(Path.Combine(root, ".owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.Delete));
            }
            Assert.False(JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(marker)).Active);
            using var available = new FileStream(Path.Combine(root, ".owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.Delete);
        }

        [Fact]
        public void ReusedPidIsNotAnOwner()
        {
            using var owner = new WorkspaceOwner(root, "run", "run", null);
            var info = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(Path.Combine(root, ".owner.json")));
            info.ProcessStartedTicks = 0;
            Assert.False(WorkspaceOwner.IsAlive(info));
        }

        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
