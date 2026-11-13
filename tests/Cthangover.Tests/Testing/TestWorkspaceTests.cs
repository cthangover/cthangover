namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Cthangover.TestRunner;

    /// <summary>Writes hit a physical copy; independent components recover modified, deleted and additional files.</summary>
    [Collection("runner-infrastructure")]
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
        public void CopiesEveryModsFolder()
        {
            foreach (var folder in ModFolders.Names)
            {
                Directory.CreateDirectory(Path.Combine(source, folder, "extra"));
                File.WriteAllText(Path.Combine(source, folder, "extra", "manifest.json"), "{\"id\":\"extra\"}");
            }

            using var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N"));

            foreach (var folder in ModFolders.Names)
                Assert.Equal("{\"id\":\"extra\"}", File.ReadAllText(Path.Combine(workspace.Root, folder, "extra", "manifest.json")));
        }

        [Theory]
        [InlineData("Directory.Build.props")]
        [InlineData("Directory.Build.targets")]
        public void BuildRuntimeSettingsAreCopiedAndRestoredExactly(string file)
        {
            var text = "<Project><PropertyGroup><RuntimeFrameworkVersion>10.0.15</RuntimeFrameworkVersion></PropertyGroup></Project>";
            File.WriteAllText(Path.Combine(source, file), text);
            using var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N"));
            workspace.Begin("first");
            var copy = Path.Combine(workspace.Root, file);
            Assert.Equal(text, File.ReadAllText(copy));
            var time = File.GetLastWriteTimeUtc(copy);
            File.WriteAllText(copy, text.Replace("10.0.15", "10.0.16"));
            File.SetLastWriteTimeUtc(copy, time);
            workspace.Begin("second");
            Assert.Equal(text, File.ReadAllText(copy));
            Assert.Equal(text, File.ReadAllText(Path.Combine(source, file)));
            Assert.False(workspace.EnvironmentVariables().ContainsKey("DOTNET_ROLL_FORWARD"));
            Assert.False(workspace.EnvironmentVariables().ContainsKey("DOTNET_HOST_PATH"));
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
        public void SkipRestoreIsExplicitAndTheNextStrictRestoreStillChecksBytes()
        {
            using var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N"), freshCache: true);
            workspace.Begin("first", restore: true);
            var copy = Path.Combine(workspace.Root, "mods", "probe", "data.json");
            var time = File.GetLastWriteTimeUtc(copy);
            File.WriteAllText(copy, "{\"value\":2}");
            File.SetLastWriteTimeUtc(copy, time);
            workspace.Begin("second", restore: false);
            Assert.Equal("{\"value\":2}", File.ReadAllText(copy));
            Assert.Equal(0, workspace.RestoreCount);
            workspace.Begin("third", restore: true);
            Assert.Equal("{\"value\":1}", File.ReadAllText(copy));
            Assert.Equal(1, workspace.RestoreCount);
            Assert.True(workspace.RestoreSeconds >= 0);
            Assert.StartsWith(Path.GetDirectoryName(workspace.Root), workspace.CacheDirectory);
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

        [Fact]
        public void SharedHomesNeverRewriteOverrideAndDetectHiddenAuthoringEdits()
        {
            using var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N"));
            workspace.ConfigureShared();
            var configuration = File.ReadAllText(Path.Combine(workspace.Root, "override.cfg"));
            using var first = workspace.SharedComponent("first");
            using var second = workspace.SharedComponent("second");
            Assert.Equal(first.Root, second.Root);
            Assert.NotEqual(first.UserDirectory, second.UserDirectory);
            Assert.NotEqual(first.TemporaryDirectory, second.TemporaryDirectory);
            Assert.Equal(configuration, File.ReadAllText(Path.Combine(workspace.Root, "override.cfg")));
            Assert.Contains("cthangover-tests/data", configuration);
            Assert.Equal(first.UserDirectory, first.EnvironmentVariables()["CTHANGOVER_TEST_DATA"]);
            var variable = OperatingSystem.IsWindows() ? "APPDATA" : OperatingSystem.IsMacOS() ? "HOME" : "XDG_DATA_HOME";
            var data = first.EnvironmentVariables()[variable];
            if (OperatingSystem.IsMacOS()) data = Path.Combine(data, "Library", "Application Support");
            Assert.Equal(Path.Combine(data, "cthangover-tests", "data"), first.UserDirectory);
            Assert.Empty(workspace.VerifyUnchanged());
            var file = Path.Combine(workspace.Root, "mods", "probe", "data.json");
            var time = File.GetLastWriteTimeUtc(file);
            File.WriteAllText(file, "{\"value\":2}");
            File.SetLastWriteTimeUtc(file, time);
            File.WriteAllText(Path.Combine(workspace.Root, "added.md"), "unexpected");
            File.Delete(Path.Combine(workspace.Root, "mods", "probe", "manifest.json"));
            Directory.CreateDirectory(Path.Combine(workspace.Root, ".godot", "generated"));
            File.WriteAllText(Path.Combine(workspace.Root, ".godot", "generated", "cache"), "ignored");
            File.WriteAllText(Path.Combine(workspace.Root, "tests", "artifacts", "probe", "output.txt"), "ignored");
            Assert.Equal(new[] { "added.md", "mods/probe/data.json", "mods/probe/manifest.json" }, workspace.VerifyUnchanged());
        }

        [Fact]
        public void SharedOutputCollectionDoesNotReadOtherLaunchDirectories()
        {
            using var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N"));
            workspace.ConfigureShared();
            using var component = workspace.SharedComponent("first");
            var artifacts = Path.Combine(workspace.Root, "tests", "artifacts");
            Directory.CreateDirectory(Path.Combine(artifacts, "other"));
            File.WriteAllText(Path.Combine(artifacts, "probe", "own.txt"), "own");
            File.WriteAllText(Path.Combine(artifacts, "other", "other.txt"), "other");
            var result = new TestResult { Passed = true };
            var destination = Path.Combine(source, "collected");
            ArtifactCollector.Collect(component, destination, result, new IntegrationTestDefinition {
                Args = new[] { "--shots=res://tests/artifacts/probe" } }, shared: true);
            Assert.Equal("own", File.ReadAllText(Path.Combine(destination, "outputs", "probe", "own.txt")));
            Assert.False(Directory.Exists(Path.Combine(destination, "outputs", "other")));
            Assert.Single(result.Artifacts);
        }

        [Fact]
        public void SharedVerificationChecksBinaryBytesEvenWithUnchangedMetadata()
        {
            var sourceFile = Path.Combine(source, "sdk", "fixtures", "image.bin");
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });
            using var workspace = new TestWorkspace(source, "contract_" + Guid.NewGuid().ToString("N"));
            workspace.ConfigureShared();
            var copy = Path.Combine(workspace.Root, "sdk", "fixtures", "image.bin");
            var time = File.GetLastWriteTimeUtc(copy);
            File.WriteAllBytes(copy, new byte[] { 3, 2, 1 });
            File.SetLastWriteTimeUtc(copy, time);
            Assert.Equal(new[] { "sdk/fixtures/image.bin" }, workspace.VerifyUnchanged());
        }

        public void Dispose() => Directory.Delete(source, true);
    }
}
