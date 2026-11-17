namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Exercises the public immutable parser and actual-path validator without native APIs, global environment
    /// changes or logger initialization. Every refusal is checked before launch output creation.
    /// </summary>
    public sealed class TestExecutionContextTests
    {
        /// <summary>Keeps production and legacy Verify-only launches outside the new identity/output protocol.</summary>
        [Fact]
        public void NormalAndLegacyVerifyOnlyLaunchesDoNotActivateContext()
        {
            Assert.Null(TestExecutionContext.Parse(new Dictionary<string, string>(), Array.Empty<string>()));
            using var fixture = new TestExecutionContextFixture();
            var legacy = new Dictionary<string, string>
            {
                ["CTHANGOVER_TEST_DATA"] = fixture.Environment["CTHANGOVER_TEST_DATA"],
                ["CTHANGOVER_TEST_PROJECT"] = fixture.Environment["CTHANGOVER_TEST_PROJECT"],
                ["CTHANGOVER_TEST_CACHE"] = fixture.Environment["CTHANGOVER_TEST_CACHE"]
            };
            var arguments = new[]
            {
                "--frontend",
                "--run-tests",
                "--mod=anim3d_base"
            };
            Assert.Null(TestExecutionContext.Parse(legacy, arguments));
            Assert.Empty(Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
        }

        /// <summary>Checks every immutable identity/root and derived sink while detecting any parser-created output.</summary>
        [Fact]
        public void CompleteContextCopiesIdentityAndDerivesDistinctContainedOutputsWithoutWrites()
        {
            using var fixture = new TestExecutionContextFixture();
            var before = Directory.GetFileSystemEntries(fixture.Root, "*", SearchOption.AllDirectories);
            var context = fixture.Parse();
            Assert.NotNull(context);
            Assert.Equal("run01", context.RunId);
            Assert.Equal("probe/read", context.TestId);
            Assert.Equal("1", context.AttemptId);
            Assert.Equal("launch01", context.LaunchId);
            Assert.Equal("worker01", context.WorkerId);
            Assert.Equal(new string('a', 64), context.PreparedRevision);
            Assert.Equal("input01", context.InputIdentity);
            Assert.Equal("lease01", context.LeaseId);
            Assert.Equal("read", context.Access);
            Assert.Equal(fixture.Environment["CTHANGOVER_TEST_ARTIFACTS"], context.ArtifactRoot);
            Assert.Equal(fixture.Environment["CTHANGOVER_TEST_LOGS"], context.LogRoot);
            Assert.Equal(fixture.Environment["CTHANGOVER_TEST_PROJECT"], context.ProjectRoot);
            Assert.Equal(fixture.Environment["CTHANGOVER_TEST_DATA"], context.UserDataRoot);
            Assert.Equal(fixture.Environment["CTHANGOVER_TEST_CACHE"], context.CacheRoot);
            Assert.Equal(fixture.Environment["TEMP"], context.TemporaryRoot);
            Assert.Equal(Path.Combine(context.LogRoot, "cthangover.log"), context.MainLogPath);
            Assert.Equal(Path.Combine(context.LogRoot, "cthangover_errors.log"), context.ErrorLogPath);
            Assert.Equal(Path.Combine(context.LogRoot, "godot.log"), context.NativeLogPath);
            Assert.Equal(Path.Combine(context.ArtifactRoot, "mod-tests"), context.ReportDirectory);
            Assert.Equal(Path.Combine(context.ArtifactRoot, "screens"), context.ScreenshotsDirectory);
            Assert.Equal(Path.Combine(context.ArtifactRoot, "sandbox"), context.SandboxDirectory);
            fixture.Environment["CTHANGOVER_TEST_RUN_ID"] = "changed";
            fixture.Environment["CTHANGOVER_TEST_ARTIFACTS"] = Path.Combine(fixture.Root, "different");
            Assert.Equal("run01", context.RunId);
            Assert.NotEqual(fixture.Environment["CTHANGOVER_TEST_ARTIFACTS"], context.ArtifactRoot);
            Assert.Equal(before, Directory.GetFileSystemEntries(fixture.Root, "*", SearchOption.AllDirectories));
        }

        /// <summary>A missing mandatory value cannot silently fall back to ordinary settings or logging.</summary>
        [Theory]
        [InlineData("CTHANGOVER_TEST_RUN_ID")]
        [InlineData("CTHANGOVER_TEST_ID")]
        [InlineData("CTHANGOVER_TEST_ATTEMPT_ID")]
        [InlineData("CTHANGOVER_TEST_LAUNCH_ID")]
        [InlineData("CTHANGOVER_TEST_WORKER_ID")]
        [InlineData("CTHANGOVER_TEST_PREPARED_REVISION")]
        [InlineData("CTHANGOVER_TEST_INPUT_IDENTITY")]
        [InlineData("CTHANGOVER_TEST_LEASE_ID")]
        [InlineData("CTHANGOVER_TEST_ACCESS")]
        [InlineData("CTHANGOVER_TEST_ARTIFACTS")]
        [InlineData("CTHANGOVER_TEST_LOGS")]
        [InlineData("CTHANGOVER_TEST_PROJECT")]
        [InlineData("CTHANGOVER_TEST_DATA")]
        [InlineData("CTHANGOVER_TEST_CACHE")]
        [InlineData("TEMP")]
        public void PartialContextRefusesBeforeAnyOutputIsCreated(string missing)
        {
            using var fixture = new TestExecutionContextFixture();
            fixture.Environment.Remove(missing);
            Assert.Throws<ArgumentException>(fixture.ParseOnly);
            Assert.Empty(Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
        }

        /// <summary>Rejects malformed identities and rooted-path violations before creating managed output.</summary>
        [Theory]
        [InlineData("CTHANGOVER_TEST_RUN_ID", "")]
        [InlineData("CTHANGOVER_TEST_ID", "probe\npeer")]
        [InlineData("CTHANGOVER_TEST_LAUNCH_ID", "../peer")]
        [InlineData("CTHANGOVER_TEST_ACCESS", "unknown")]
        [InlineData("CTHANGOVER_TEST_ARTIFACTS", "relative/output")]
        [InlineData("CTHANGOVER_TEST_PROJECT", "relative/project")]
        public void InvalidIdentityAccessOrRelativePathsFailClosed(string key, string value)
        {
            using var fixture = new TestExecutionContextFixture();
            fixture.Environment[key] = value;
            Assert.Throws<ArgumentException>(fixture.ParseOnly);
            Assert.Empty(Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
        }

        /// <summary>App argv may corroborate env identity but can never override it or duplicate a mirror.</summary>
        [Fact]
        public void OptionalMirrorsAcceptEqualSplitValuesAndRefuseDuplicatesOrConflicts()
        {
            using var fixture = new TestExecutionContextFixture();
            fixture.Arguments.AddRange(new[]
            {
                "--frontend",
                "--test-run-id=run01",
                "--test-test-id",
                "probe/read",
                "--test-attempt-id=1",
                "--test-launch-id=launch01",
                "--test-worker-id=worker01",
                "--test-output-dir=" + fixture.Environment["CTHANGOVER_TEST_ARTIFACTS"],
                "--test-log-dir=" + fixture.Environment["CTHANGOVER_TEST_LOGS"]
            });
            Assert.Equal("run01", fixture.Parse().RunId);
            fixture.Arguments.Add("--test-run-id=run01");
            Assert.Throws<ArgumentException>(fixture.ParseOnly);
            fixture.Arguments.RemoveAt(fixture.Arguments.Count - 1);
            fixture.Arguments[1] = "--test-run-id=peer-run";
            Assert.Throws<ArgumentException>(fixture.ParseOnly);
            Assert.Empty(Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
        }

        /// <summary>Conflicting temp aliases would split native and managed writes, so parsing must refuse them.</summary>
        [Theory]
        [InlineData("TMP")]
        [InlineData("TMPDIR")]
        public void SuppliedTemporaryAliasesMustMatchTheAuthoritativeTemporaryRoot(string alias)
        {
            using var fixture = new TestExecutionContextFixture();
            fixture.Environment[alias] = Path.Combine(fixture.Root, "peer-temp");
            Assert.Throws<ArgumentException>(fixture.ParseOnly);
        }

        /// <summary>Tests real containment boundaries rather than string prefixes and protects all immutable/shared roots.</summary>
        [Fact]
        public void LogSiblingPrefixAndInputDataCacheOverlapsAreRejected()
        {
            using var fixture = new TestExecutionContextFixture();
            var output = fixture.Environment["CTHANGOVER_TEST_ARTIFACTS"];
            fixture.Environment["CTHANGOVER_TEST_LOGS"] = output;
            Assert.Throws<ArgumentException>(fixture.ParseOnly);
            fixture.Environment["CTHANGOVER_TEST_LOGS"] = output + "_peer" + Path.DirectorySeparatorChar + "logs";
            Assert.Throws<ArgumentException>(fixture.ParseOnly);
            fixture.Environment["CTHANGOVER_TEST_LOGS"] = Path.Combine(output, "logs");
            var protectedKeys = new[]
            {
                "CTHANGOVER_TEST_PROJECT",
                "CTHANGOVER_TEST_DATA",
                "CTHANGOVER_TEST_CACHE"
            };
            foreach (var key in protectedKeys)
            {
                var original = fixture.Environment[key];
                fixture.Environment[key] = output;
                Assert.Throws<ArgumentException>(fixture.ParseOnly);
                fixture.Environment[key] = original;
                var temporary = fixture.Environment["TEMP"];
                fixture.Environment["TEMP"] = original;
                fixture.Environment["TMP"] = original;
                fixture.Environment["TMPDIR"] = original;
                Assert.Throws<ArgumentException>(fixture.ParseOnly);
                fixture.Environment["TEMP"] = temporary;
                fixture.Environment["TMP"] = temporary;
                fixture.Environment["TMPDIR"] = temporary;
            }

            Assert.Empty(Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
        }

        /// <summary>Validates actual engine roots and visible native sink independently of preparation labels.</summary>
        [Fact]
        public void RuntimePathAndVisibleNativeArgumentEvidenceIsExactAndDoesNotWrite()
        {
            using var fixture = new TestExecutionContextFixture();
            var context = fixture.Parse();
            var native = new[]
            {
                "--path",
                context.ProjectRoot,
                "--log-file",
                context.NativeLogPath
            };
            Assert.Equal("engine-argument", context.ValidateRuntimePaths(context.ProjectRoot,
                context.UserDataRoot, context.TemporaryRoot, native));
            fixture.BindRuntime(context, native);
            var validation = fixture;
            validation.Project += "_peer";
            Assert.Throws<ArgumentException>(validation.Validate);
            validation.Project = context.ProjectRoot;
            validation.Data += "_peer";
            Assert.Throws<ArgumentException>(validation.Validate);
            validation.Data = context.UserDataRoot;
            validation.Temporary += "_peer";
            Assert.Throws<ArgumentException>(validation.Validate);
            Assert.Empty(Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
        }

        /// <summary>A consumed engine flag needs matching pre-start receipt evidence; malformed receipts cannot authorize sinks.</summary>
        [Fact]
        public void ConsumedNativeFlagRequiresMatchingParentReceiptAndDoesNotTreatItAsPass()
        {
            using var fixture = new TestExecutionContextFixture();
            var context = fixture.Parse();
            var receipt = fixture.CreateReceipt(context);
            var native = new[]
            {
                "--path",
                context.ProjectRoot
            };
            Assert.Equal("parent-launch-receipt", context.ValidateRuntimePaths(context.ProjectRoot,
                context.UserDataRoot, context.TemporaryRoot, native, receipt));
            fixture.BindRuntime(context, native);
            var validation = fixture;
            Assert.Throws<ArgumentException>(validation.Validate);
            var document = JsonNode.Parse(receipt);
            document["launchId"] = "peer-launch";
            validation.Receipt = document.ToJsonString();
            Assert.Throws<ArgumentException>(validation.Validate);
            document["launchId"] = context.LaunchId;
            document["nativeLogPath"] = context.MainLogPath;
            validation.Receipt = document.ToJsonString();
            Assert.Throws<ArgumentException>(validation.Validate);
            validation.Receipt = "not-json";
            Assert.Throws<ArgumentException>(validation.Validate);
            Assert.Empty(Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories));
        }
    }
}
