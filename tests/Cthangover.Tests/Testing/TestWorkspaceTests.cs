namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Cthangover.TestRunner;

    /// <summary>Physical write lanes restore changed, deleted and additional files while preserving borrowed prepared inputs.</summary>
    [Collection("runner-infrastructure")]
    public class TestWorkspaceTests : IDisposable
    {
        private readonly string source = Path.Combine(Path.GetTempPath(), "cthangover-workspace-" + Guid.NewGuid().ToString("N"));
        private readonly List<PreparedInputLease> leases = new();

        /// <summary>Creates tiny real authoring, SDK-fixture and declared-output inputs in an independently owned source tree.</summary>
        public TestWorkspaceTests()
        {
            Directory.CreateDirectory(Path.Combine(source, "mods", "probe"));
            File.WriteAllText(Path.Combine(source, "project.godot"), "config_version=5\n");
            File.WriteAllText(Path.Combine(source, "mods", "probe", "manifest.json"), "{\"id\":\"probe\"}");
            File.WriteAllText(Path.Combine(source, "mods", "probe", "data.json"), "{\"value\":1}");
            Directory.CreateDirectory(Path.Combine(source, "sdk", "fixtures"));
            File.WriteAllText(Path.Combine(source, "sdk", "fixtures", "model.json"), "{\"model\":1}");
            Directory.CreateDirectory(Path.Combine(source, "tests"));
            var test = new IntegrationTestDefinition
            {
                Name = "probe",
                Scene = "res://probe.tscn",
                Result = new ResultContract { Name = "probe" },
                Args = new[] { "--shots=res://tests/artifacts/probe" },
                Owners = new[] { "core" },
                Profiles = new[] { "full" }
            };
            var manifest = new TestManifest
            {
                SchemaVersion = 2,
                Unit = new UnitManifest { Project = "tests/unit.csproj" },
                Integration = new() { test }
            };
            File.WriteAllText(Path.Combine(source, "tests", "tests.manifest.json"),
                JsonSerializer.Serialize(manifest, ReportWriter.JsonOptions));
        }

        /// <summary>Every supported mod root remains a physical input; public/private layout does not remove authoring data.</summary>
        [Fact]
        public void CopiesEveryModsFolder()
        {
            foreach (var folder in ModFolders.Names)
            {
                Directory.CreateDirectory(Path.Combine(source, folder, "extra"));
                File.WriteAllText(Path.Combine(source, folder, "extra", "manifest.json"), "{\"id\":\"extra\"}");
            }

            using var workspace = Workspace();
            foreach (var folder in ModFolders.Names)
            {
                Assert.Equal("{\"id\":\"extra\"}", File.ReadAllText(Path.Combine(workspace.Root, folder, "extra", "manifest.json")));
            }
        }

        /// <summary>Restore compares build-input bytes even when an authoring edit preserves its original timestamp.</summary>
        [Theory]
        [InlineData("Directory.Build.props")]
        [InlineData("Directory.Build.targets")]
        public void BuildRuntimeSettingsAreCopiedAndRestoredExactly(string file)
        {
            var text = "<Project><PropertyGroup><RuntimeFrameworkVersion>10.0.15</RuntimeFrameworkVersion></PropertyGroup></Project>";
            File.WriteAllText(Path.Combine(source, file), text);
            using var workspace = Workspace();
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

        /// <summary>Restores delete/add/overwrite deltas while keeping source bytes and successive component DATA independent.</summary>
        [Fact]
        public void RestoresAuthoringFilesAndKeepsOriginalCheckoutUntouched()
        {
            using var workspace = Workspace();
            workspace.Begin("first");
            Assert.Equal("{\"model\":1}", File.ReadAllText(Path.Combine(workspace.Root, "sdk", "fixtures", "model.json")));
            using var output = new LaunchExecutionContext(workspace, "run01", "authoring", "worker01", source + "_outputs");
            Assert.True(Directory.Exists(OutputPath(output)));
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
            Assert.True(workspace.Metrics.FilesRemoved > 0);
            Assert.NotEqual(firstUser, workspace.UserDirectory);
            Assert.False(File.Exists(Path.Combine(workspace.UserDirectory, "settings.json")));
            Assert.True(File.Exists(Path.Combine(firstUser, "settings.json")));
            Assert.Contains("config/custom_user_dir_name=\"cthangover-tests/",
                File.ReadAllText(Path.Combine(workspace.Root, "override.cfg")));
            Assert.Equal(workspace.UserDirectory, workspace.EnvironmentVariables()["CTHANGOVER_TEST_DATA"]);
            Assert.Empty(workspace.Inputs.VerifyUnchanged());
        }

        /// <summary>An explicit restore skip retains dependency changes; the next strict boundary verifies real bytes.</summary>
        [Fact]
        public void SkipRestoreIsExplicitAndTheNextStrictRestoreStillChecksBytes()
        {
            using var workspace = Workspace(freshCache: true);
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
            Assert.True(workspace.Metrics.RestoreFiles > 0);
            Assert.True(workspace.Metrics.RestoreBytes > 0);
            Assert.StartsWith(workspace.RunRoot + Path.DirectorySeparatorChar, workspace.CacheDirectory);
        }

        /// <summary>Disposal removes run homes and DATA; reusable physical inputs remain protected by their own lease.</summary>
        [Fact]
        public void SuccessfulDisposalRemovesOwnedRunDataAndPreservesReusableInputs()
        {
            string project;
            string user;
            string run;
            using (var workspace = Workspace())
            {
                workspace.Begin("probe");
                project = workspace.Root;
                user = workspace.UserDirectory;
                run = workspace.RunRoot;
            }

            Assert.True(Directory.Exists(project));
            Assert.False(Directory.Exists(user));
            Assert.False(Directory.Exists(run));
            Assert.True(Directory.Exists(source));
        }

        /// <summary>Shared components never rewrite fixed override settings and hidden byte changes remain authoring mutations.</summary>
        [Fact]
        public void SharedHomesNeverRewriteOverrideAndDetectHiddenAuthoringEdits()
        {
            using var workspace = Workspace();
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
            if (OperatingSystem.IsMacOS())
            {
                data = Path.Combine(data, "Library", "Application Support");
            }

            Assert.Equal(Path.Combine(data, "cthangover-tests", "data"), first.UserDirectory);
            Assert.Empty(workspace.VerifyUnchanged());
            var file = Path.Combine(workspace.Root, "mods", "probe", "data.json");
            var time = File.GetLastWriteTimeUtc(file);
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            File.WriteAllText(file, "{\"value\":2}");
            File.SetLastWriteTimeUtc(file, time);
            File.WriteAllText(Path.Combine(workspace.Root, "added.md"), "unexpected");
            var deleted = Path.Combine(workspace.Root, "mods", "probe", "manifest.json");
            File.SetAttributes(deleted, File.GetAttributes(deleted) & ~FileAttributes.ReadOnly);
            File.Delete(deleted);
            Directory.CreateDirectory(Path.Combine(workspace.Root, ".godot", "generated"));
            File.WriteAllText(Path.Combine(workspace.Root, ".godot", "generated", "cache"), "ignored");
            Directory.CreateDirectory(Path.Combine(workspace.Root, "tests", "artifacts", "probe"));
            File.WriteAllText(Path.Combine(workspace.Root, "tests", "artifacts", "probe", "output.txt"), "ignored");
            var expected = new[]
            {
                ".godot/generated/cache",
                "added.md",
                "mods/probe/data.json",
                "mods/probe/manifest.json",
                "tests/artifacts/probe/output.txt"
            };
            Assert.Equal(expected, workspace.VerifyUnchanged());
        }

        /// <summary>Collection preserves only a launch's declared project output scope, excluding neighboring launch directories.</summary>
        [Fact]
        public void SharedOutputCollectionDoesNotReadOtherLaunchDirectories()
        {
            using var workspace = Workspace();
            workspace.ConfigureShared();
            using var component = workspace.SharedComponent("first");
            using var peerComponent = workspace.SharedComponent("second");
            using var own = new LaunchExecutionContext(component, "run01", "own", "worker01", source + "_outputs");
            using var peer = new LaunchExecutionContext(peerComponent, "run01", "peer", "worker02", source + "_outputs");
            var ownOutput = OutputPath(own);
            var peerOutput = OutputPath(peer);
            var ownFile = Path.Combine(ownOutput, "own.txt");
            File.WriteAllText(ownFile, "own");
            File.WriteAllText(Path.Combine(peerOutput, "peer.txt"), "peer");
            var result = new TestResult
            {
                Passed = true,
                Execution = own.Metrics
            };
            var destination = source + "_outputs";
            var definition = new IntegrationTestDefinition { Args = new[] { "--shots=res://tests/artifacts/probe" } };
            ArtifactCollector.Collect(component, destination, result, definition, shared: true);
            Assert.Equal("own", File.ReadAllText(ownFile));
            Assert.Contains(ownFile, result.Artifacts);
            foreach (var artifact in result.Artifacts)
            {
                Assert.StartsWith(own.Metrics.OutputDirectory + Path.DirectorySeparatorChar, artifact);
                Assert.False(artifact.StartsWith(peer.Metrics.OutputDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            }

            var expected = Directory.GetFiles(own.Metrics.OutputDirectory, "*", SearchOption.AllDirectories);
            var actual = result.Artifacts.ToArray();
            Array.Sort(expected, StringComparer.Ordinal);
            Array.Sort(actual, StringComparer.Ordinal);
            Assert.Equal(expected, actual);
            Assert.Equal("peer", File.ReadAllText(Path.Combine(peerOutput, "peer.txt")));
            Assert.Empty(workspace.Inputs.VerifyUnchanged());
        }

        /// <summary>Binary edits with identical length and timestamps are still reported by the public integrity fence.</summary>
        [Fact]
        public void SharedVerificationChecksBinaryBytesEvenWithUnchangedMetadata()
        {
            var sourceFile = Path.Combine(source, "sdk", "fixtures", "image.bin");
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });
            using var workspace = Workspace();
            workspace.ConfigureShared();
            var copy = Path.Combine(workspace.Root, "sdk", "fixtures", "image.bin");
            var time = File.GetLastWriteTimeUtc(copy);
            File.SetAttributes(copy, File.GetAttributes(copy) & ~FileAttributes.ReadOnly);
            File.WriteAllBytes(copy, new byte[] { 3, 2, 1 });
            File.SetLastWriteTimeUtc(copy, time);
            Assert.Equal(new[] { "sdk/fixtures/image.bin" }, workspace.VerifyUnchanged());
        }

        /// <summary>Directory-only lane deltas are removed bottom-up without recopying intact input bytes or declared empty dirs.</summary>
        [Fact]
        public void WriteRestoreRemovesUnexpectedEmptyLeafAndKeepsDeclaredEmptyDirectory()
        {
            var declared = Path.Combine("mods", "probe", "declared_empty");
            var unexpected = Path.Combine("mods", "unexpected_empty");
            Directory.CreateDirectory(Path.Combine(source, declared));
            using var workspace = Workspace();
            workspace.Begin("first");
            Assert.True(Directory.Exists(Path.Combine(workspace.Root, declared)));
            Assert.Empty(workspace.VerifyUnchanged());
            var filesCopied = workspace.Metrics.FilesCopied;
            var bytesCopied = workspace.Metrics.BytesCopied;
            var fullCopies = workspace.Metrics.FullProjectCopies;
            Directory.CreateDirectory(Path.Combine(workspace.Root, unexpected));
            Assert.Equal(new[] { "mods/unexpected_empty" }, workspace.VerifyUnchanged());
            workspace.Begin("second");
            Assert.False(Directory.Exists(Path.Combine(workspace.Root, unexpected)));
            Assert.True(Directory.Exists(Path.Combine(workspace.Root, declared)));
            Assert.Equal(filesCopied, workspace.Metrics.FilesCopied);
            Assert.Equal(bytesCopied, workspace.Metrics.BytesCopied);
            Assert.Equal(fullCopies, workspace.Metrics.FullProjectCopies);
            Assert.False(Directory.Exists(Path.Combine(source, unexpected)));
            Assert.False(Directory.Exists(Path.Combine(workspace.Inputs.Root, unexpected)));
            Assert.Empty(workspace.VerifyUnchanged());
            Assert.Empty(workspace.Inputs.VerifyUnchanged());
        }

        /// <summary>A created empty directory is retained as a directory diagnostic, never falsely reported as a deleted file.</summary>
        [Fact]
        public void CaptureChangesRetainsAddedEmptyDirectoryAndUsesItsOwnManifest()
        {
            using var workspace = Workspace();
            workspace.Begin("first");
            Assert.Empty(workspace.VerifyUnchanged());
            Directory.CreateDirectory(Path.Combine(workspace.Root, "mods", "unexpected_empty"));
            var destination = Path.Combine(source, "captured-empty");
            var artifacts = workspace.CaptureChanges(destination);
            var manifest = Path.Combine(destination, "changed-directories.json");
            Assert.Contains(manifest, artifacts);
            Assert.True(Directory.Exists(Path.Combine(destination, "mods", "unexpected_empty")));
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(destination, "mods", "unexpected_empty")));
            var directories = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(manifest), ReportWriter.JsonOptions);
            Assert.Equal(new[] { "mods/unexpected_empty" }, directories);
            var json = JsonSerializer.Serialize(directories, ReportWriter.JsonOptions);
            var restored = JsonSerializer.Deserialize<List<string>>(json, ReportWriter.JsonOptions);
            Assert.Equal(json, JsonSerializer.Serialize(restored, ReportWriter.JsonOptions));
            var deleted = Path.Combine(destination, "deleted-files.json");
            if (File.Exists(deleted))
            {
                var deletedFiles = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(deleted), ReportWriter.JsonOptions);
                Assert.NotNull(deletedFiles);
                Assert.Empty(deletedFiles);
            }

            Assert.Equal(new[] { "mods/unexpected_empty" }, workspace.VerifyUnchanged());
        }

        /// <summary>Writable lanes track obj empty-leaf mutations and restore them bottom-up.</summary>
        [Fact]
        public void WriteLaneTracksObjEmptyLeafAndRestoresItsParentsBottomUp()
        {
            using var workspace = Workspace();
            workspace.Begin("first");
            Assert.Empty(workspace.VerifyUnchanged());
            var filesCopied = workspace.Metrics.FilesCopied;
            var bytesCopied = workspace.Metrics.BytesCopied;
            Directory.CreateDirectory(Path.Combine(workspace.Root, "obj", "extra_parent", "unexpected_empty"));
            Assert.Equal(new[] { "obj/extra_parent/unexpected_empty" }, workspace.VerifyUnchanged());
            workspace.Begin("second");
            Assert.False(Directory.Exists(Path.Combine(workspace.Root, "obj", "extra_parent")));
            Assert.Equal(filesCopied, workspace.Metrics.FilesCopied);
            Assert.Equal(bytesCopied, workspace.Metrics.BytesCopied);
            Assert.Empty(workspace.VerifyUnchanged());
            Assert.Empty(workspace.Inputs.VerifyUnchanged());
        }

        private static string OutputPath(LaunchExecutionContext context)
        {
            var arguments = context.UserArguments(new[] { "--", "--shots=res://tests/artifacts/probe" });
            foreach (var argument in arguments)
            {
                if (argument.StartsWith("--shots=", StringComparison.Ordinal))
                {
                    var path = argument.Substring("--shots=".Length);
                    Assert.True(Path.IsPathFullyQualified(path));
                    Assert.StartsWith(context.Metrics.OutputDirectory + Path.DirectorySeparatorChar, path);
                    return path;
                }
            }

            throw new InvalidOperationException("Declared screenshot output was lost from child arguments.");
        }

        private TestWorkspace Workspace(bool freshCache = false)
        {
            var inputs = PreparedInputStore.Acquire(source, source + "_cache");
            leases.Add(inputs);
            return new TestWorkspace(inputs, "contract_" + Guid.NewGuid().ToString("N"), writable: true, freshCache: freshCache);
        }

        /// <summary>Releases borrowed prepared owners before deleting only this fixture's source and persistent lane cache.</summary>
        public void Dispose()
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }

            Directory.Delete(source, true);
            if (Directory.Exists(source + "_outputs"))
            {
                Directory.Delete(source + "_outputs", true);
            }
            if (Directory.Exists(source + "_cache"))
            {
                foreach (var path in Directory.GetFiles(source + "_cache", "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                }

                Directory.Delete(source + "_cache", true);
            }
        }
    }
}
