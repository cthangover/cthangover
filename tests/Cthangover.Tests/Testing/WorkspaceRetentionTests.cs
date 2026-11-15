namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.TestRunner;

    public sealed class WorkspaceRetentionTests : IDisposable
    {
        private readonly string temporary = Path.Combine(Path.GetTempPath(), "retention_contract_" + Guid.NewGuid().ToString("N"));
        private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
        private string Root => Path.Combine(temporary, "cthangover-tests");
        private string Home => Path.Combine(temporary, "home");

        private string Copy(string id, double days = 0, int bytes = 100)
        {
            var path = Path.Combine(Root, id);
            var group = System.Text.RegularExpressions.Regex.Replace(id, "_(shared|write[0-9]+)$", "");
            using (var owner = new WorkspaceOwner(path, id, group, Path.Combine(temporary, "reports"))) { }
            var marker = Path.Combine(path, ".owner.json");
            var info = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(marker));
            info.UpdatedAt = now.AddDays(-days);
            File.WriteAllText(marker, JsonSerializer.Serialize(info));
            Directory.CreateDirectory(Path.Combine(path, "project"));
            File.WriteAllBytes(Path.Combine(path, "project", "asset.bin"), new byte[bytes]);
            return path;
        }

        private void Prune(int count = 3, double days = 3, long bytes = long.MaxValue)
            => WorkspaceRetention.Prune(temporary, count, days, bytes, now, new[] { Home });

        [Fact]
        public void ExpiresOldRunAndSavesLogsBeforeDeletingPairedUserData()
        {
            var path = Copy("old", 4);
            var user = Path.Combine(Home, "cthangover-tests", "old", "component", "logs");
            Directory.CreateDirectory(user); File.WriteAllText(Path.Combine(user, "game.log"), "failure evidence");
            Prune();
            Assert.False(Directory.Exists(path));
            Assert.False(Directory.Exists(Path.Combine(Home, "cthangover-tests", "old")));
            var logs = Directory.GetFiles(Path.Combine(temporary, "reports"), "game.log", SearchOption.AllDirectories);
            Assert.Single(logs); Assert.Equal("failure evidence", File.ReadAllText(logs[0]));
            Assert.Empty(Directory.GetFiles(Path.Combine(temporary, "reports"), "asset.bin", SearchOption.AllDirectories));
        }

        [Fact]
        public void NewestCountCountsLogicalRunsRatherThanLanes()
        {
            var old = Copy("older_write0", 1);
            var a = Copy("newer_write0", 0.1); var b = Copy("newer_shared", 0.1);
            Prune(count: 1);
            Assert.False(Directory.Exists(old));
            Assert.True(Directory.Exists(a)); Assert.True(Directory.Exists(b));
        }

        [Fact]
        public void ByteBudgetEvictsOldestFirst()
        {
            var old = Copy("older", 1, 10000); var recent = Copy("recent", 0.1, 1000);
            Prune(bytes: 3000);
            Assert.False(Directory.Exists(old)); Assert.True(Directory.Exists(recent));
        }

        [Fact]
        public void OneActiveLaneProtectsAllLanesOfItsRun()
        {
            var retained = Copy("active_write0", 4);
            using var owner = new WorkspaceOwner(Path.Combine(Root, "active_shared"), "active_shared", "active", null);
            Prune(count: 0, days: 0, bytes: 0);
            Assert.True(Directory.Exists(retained));
            Assert.True(Directory.Exists(Path.Combine(Root, "active_shared")));
        }

        [Fact]
        public void DeadOwnerAfterAbruptExitDoesNotPreventPruning()
        {
            var path = Copy("killed", 4); var marker = Path.Combine(path, ".owner.json");
            var info = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(marker));
            info.Active = true; info.ProcessId = int.MaxValue;
            File.WriteAllText(marker, JsonSerializer.Serialize(info));
            Prune(); Assert.False(Directory.Exists(path));
        }

        [Fact]
        public void CacheAndUnownedLegacyTreesAreNotTreatedAsRetainedRuns()
        {
            Directory.CreateDirectory(Path.Combine(Root, "cache"));
            Directory.CreateDirectory(Path.Combine(Root, "legacy"));
            File.WriteAllText(Path.Combine(Root, "cache", "pair.dll"), "cache");
            Copy("expired", 4); Prune(count: 0, bytes: 0);
            Assert.Equal("cache", File.ReadAllText(Path.Combine(Root, "cache", "pair.dll")));
            Assert.True(Directory.Exists(Path.Combine(Root, "legacy")));
        }

        [Fact]
        public void MalformedOwnerPreservesRunInsteadOfGuessingOwnership()
        {
            var path = Copy("invalid", 4); File.WriteAllText(Path.Combine(path, ".owner.json"), "broken");
            Prune(count: 0); Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void DiagnosticFailurePreservesProjectAndData()
        {
            var path = Copy("blocked", 4); var marker = Path.Combine(path, ".owner.json");
            var info = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(File.ReadAllText(marker));
            info.ArtifactsDirectory = Path.Combine(path, "project", "reports");
            File.WriteAllText(marker, JsonSerializer.Serialize(info));
            Prune(); Assert.True(File.Exists(Path.Combine(path, "project", "asset.bin")));
        }

        public void Dispose() { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }
}
