namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.TestRunner;

    public sealed class WorkspaceOwnerInfoTests
    {
        [Fact]
        public void MarkerRoundTripPreservesOwnerAndDiagnosticDestination()
        {
            var original = new WorkspaceOwnerInfo { RunId = "run_write0", GroupId = "run", ProcessId = 42,
                ProcessStartedTicks = 1234567, Active = true, UpdatedAt = DateTimeOffset.UtcNow,
                ArtifactsDirectory = "reports/run", UserDirectories = new List<string> { "data/a", "data/b" } };
            var restored = JsonSerializer.Deserialize<WorkspaceOwnerInfo>(JsonSerializer.Serialize(original));
            Assert.Equal(original.RunId, restored.RunId);
            Assert.Equal(original.GroupId, restored.GroupId);
            Assert.Equal(original.ProcessId, restored.ProcessId);
            Assert.Equal(original.ProcessStartedTicks, restored.ProcessStartedTicks);
            Assert.Equal(original.Active, restored.Active);
            Assert.Equal(original.UpdatedAt, restored.UpdatedAt);
            Assert.Equal(original.ArtifactsDirectory, restored.ArtifactsDirectory);
            Assert.Equal(original.UserDirectories, restored.UserDirectories);
        }
    }
}
