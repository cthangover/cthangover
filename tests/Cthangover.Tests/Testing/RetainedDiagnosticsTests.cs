namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Linq;
    using Cthangover.TestRunner;

    public sealed class RetainedDiagnosticsTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "diagnostics_contract_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void CopiesResultsAndLogTailWithoutCopyingProjectOrSandboxPayload()
        {
            var run = Path.Combine(root, "run"); var home = Path.Combine(run, "homes", "data");
            Directory.CreateDirectory(home); Directory.CreateDirectory(Path.Combine(run, "project"));
            File.WriteAllText(Path.Combine(run, "project", "never-copy.cs"), "source");
            File.WriteAllText(Path.Combine(home, "case.result"), "exit=139");
            File.WriteAllBytes(Path.Combine(home, "payload.bin"), new byte[10]);
            using (var output = File.Create(Path.Combine(home, "large.log")))
            { output.SetLength(8 * 1024 * 1024 + 200); output.Seek(-4, SeekOrigin.End); output.Write("tail"u8); }
            var report = Path.Combine(root, "reports");
            RetainedDiagnostics.Save(run, new WorkspaceOwnerInfo { RunId = "run", ArtifactsDirectory = report }, Array.Empty<string>());
            var files = Directory.GetFiles(report, "*", SearchOption.AllDirectories);
            Assert.DoesNotContain(files, path => path.EndsWith("never-copy.cs") || path.EndsWith("payload.bin"));
            Assert.Equal("exit=139", File.ReadAllText(files.Single(path => path.EndsWith("case.result"))));
            var log = files.Single(path => path.EndsWith("large.log"));
            Assert.Equal(8 * 1024 * 1024, new FileInfo(log).Length);
            using var input = File.OpenRead(log); input.Seek(-4, SeekOrigin.End);
            var tail = new byte[4]; Assert.Equal(4, input.Read(tail)); Assert.Equal("tail", System.Text.Encoding.UTF8.GetString(tail));
            Assert.Contains("truncated", File.ReadAllText(files.Single(path => path.EndsWith("manifest.json"))));
        }

        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
