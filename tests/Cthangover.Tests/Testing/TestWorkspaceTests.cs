namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Cthangover.TestRunner;

    /// <summary>Writes hit a physical copy; independent components recover modified, deleted and additional files.</summary>
    public class TestWorkspaceTests : IDisposable
    {
        private readonly string source = Path.Combine(Path.GetTempPath(), "workspace_contract_" + Guid.NewGuid().ToString("N"));

        public TestWorkspaceTests()
        {
            Directory.CreateDirectory(Path.Combine(source, "mods", "probe"));
            File.WriteAllText(Path.Combine(source, "project.godot"), "config_version=5\n");
            File.WriteAllText(Path.Combine(source, "mods", "probe", "manifest.json"), "{\"id\":\"probe\"}");
            File.WriteAllText(Path.Combine(source, "mods", "probe", "data.json"), "{\"value\":1}");
            Directory.CreateDirectory(Path.Combine(source, "sdk", "fixtures"));
            File.WriteAllText(Path.Combine(source, "sdk", "fixtures", "model.json"), "{\"model\":1}");
            Directory.CreateDirectory(Path.Combine(source, "tests"));
            var manifest = new TestManifest { SchemaVersion = 2, Unit = new UnitManifest { Project = "tests/unit.csproj" }, Integration = new()
            {
                new IntegrationTestDefinition { Name = "probe", Scene = "res://probe.tscn", Result = new ResultContract { Name = "probe" },
                    Args = new[] { "--shots=res://tests/artifacts/probe" }, Owners = new[] { "core" }, Profiles = new[] { "full" } },
            } };
            File.WriteAllText(Path.Combine(source, "tests", "tests.manifest.json"), JsonSerializer.Serialize(manifest, ReportWriter.JsonOptions));
        }

        [Fact]
        public void RestoresAuthoringFilesAndKeepsOriginalCheckoutUntouched()
        {
            using var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N"));
            workspace.Begin("first");
            Assert.Equal("{\"model\":1}", File.ReadAllText(Path.Combine(workspace.Root, "sdk", "fixtures", "model.json")));
            Assert.True(Directory.Exists(Path.Combine(workspace.Root, "tests", "artifacts", "probe")));
            var original = Path.Combine(source, "mods", "probe", "data.json");
            var copy = Path.Combine(workspace.Root, "mods", "probe", "data.json");
            var time = File.GetLastWriteTimeUtc(copy);
            File.WriteAllText(copy, "{\"value\":2}");
            File.SetLastWriteTimeUtc(copy, time);
            File.Delete(Path.Combine(workspace.Root, "mods", "probe", "manifest.json"));
            var extra = Path.Combine(workspace.Root, "mods", "probe", "extra.json");
            File.WriteAllText(extra, "{}");
            var failedState = Path.Combine(source, "captured-failure");
            var artifacts = workspace.CaptureChanges(failedState);
            Assert.Contains(Path.Combine(failedState, "mods", "probe", "data.json"), artifacts);
            Assert.Equal("{\"value\":2}", File.ReadAllText(Path.Combine(failedState, "mods", "probe", "data.json")));
            Assert.Contains("manifest.json", File.ReadAllText(Path.Combine(failedState, "deleted-files.json")));
            var firstUser = workspace.UserDirectory;
            File.WriteAllText(Path.Combine(firstUser, "settings.json"), "private");
            workspace.Begin("second");
            Assert.Equal("{\"value\":1}", File.ReadAllText(original));
            Assert.Equal(File.ReadAllText(original), File.ReadAllText(copy));
            Assert.True(File.Exists(Path.Combine(workspace.Root, "mods", "probe", "manifest.json")));
            Assert.False(File.Exists(extra));
            Assert.NotEqual(firstUser, workspace.UserDirectory);
            Assert.False(File.Exists(Path.Combine(workspace.UserDirectory, "settings.json")));
            Assert.True(File.Exists(Path.Combine(firstUser, "settings.json")));
            Assert.Contains("config/custom_user_dir_name=\"cthangover-tests/", File.ReadAllText(Path.Combine(workspace.Root, "override.cfg")));
            Assert.Equal(workspace.UserDirectory, workspace.EnvironmentVariables()["CTHANGOVER_TEST_DATA"]);
        }

        [Fact]
        public void SuccessfulDisposalRemovesOwnedProjectAndData()
        {
            string project, user;
            using (var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N")))
            {
                workspace.Begin("probe");
                project = workspace.Root;
                user = workspace.UserDirectory;
            }
            Assert.False(Directory.Exists(project));
            Assert.False(Directory.Exists(user));
            Assert.True(Directory.Exists(source));
        }

        public void Dispose() => Directory.Delete(source, true);
    }
}
