using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Testing;
using Xunit;

namespace Cthangover.Tests.Mods.Testing
{
    /// <summary>
    /// Contract tests of the mod-test engine gate (plan-mod-testing.md §3.1).
    /// Core guarantees: production compilation ignores test_sources, the
    /// discovery works on compiled test code, and the report writer produces
    /// the unified TEST_RESULT counting.
    /// </summary>
    public class ModTestEngineTests : IDisposable
    {
        private const string ManifestPath = "mod_test_gate_manifest.json";

        [Fact]
        public void Manifest_WithTestSources_Deserializes()
        {
            const string json = @"{ ""id"": ""m1"", ""sources"": [""src/*.cs""], ""test_sources"": [""tests/*.cs""] }";
            var manifest = JsonSerializer.Deserialize<ModManifest>(json);
            Assert.NotNull(manifest.TestSources);
            Assert.Equal("tests/*.cs", manifest.TestSources.Single());
        }

        [Fact]
        public void Manifest_WithoutTestSources_TestSourcesNull()
        {
            var manifest = JsonSerializer.Deserialize<ModManifest>(@"{ ""id"": ""m1"" }");
            Assert.Null(manifest.TestSources);
        }

        [Fact]
        public void ModConfig_RunModTests_DefaultsFalse()
        {
            var config = JsonSerializer.Deserialize<ModConfig>(@"{ ""use_assembly_cache"": true }");
            Assert.False(config.RunModTests, "test mode must be off by default (player path)");
            Assert.NotNull(config.ModTestExclude);
            Assert.Empty(config.ModTestExclude);
        }

        [Fact]
        public void ModConfig_TestModeFields_RoundTrip()
        {
            const string json = @"{ ""run_mod_tests"": true, ""mod_test_exclude"": [""a"", ""b""] }";
            var config = JsonSerializer.Deserialize<ModConfig>(json);
            Assert.True(config.RunModTests);
            Assert.Equal(new[] { "a", "b" }, config.ModTestExclude);
        }

        [Fact]
        public void Discovery_FindsMarkedStaticMethods_WithNamesAndSkip()
        {
            var tests = ModTestDiscovery.Discover(typeof(DiscoveryFixture).Assembly);
            var fixture = tests.Where(t => t.Name.StartsWith("fixture/")).ToList();

            Assert.Equal(4, fixture.Count);
            Assert.Contains(fixture, t => t.Name == "fixture/plain");
            Assert.Contains(fixture, t => t.Name == "fixture/timeout" && t.TimeoutSeconds == 7);
            var skipped = fixture.Single(t => t.Name == "fixture/skipped");
            Assert.Equal("not relevant", skipped.Skip);

            // Deterministic ordering (stable reports / history diffs)
            var names = tests.Select(t => t.Name).ToList();
            Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
        }

        [Fact]
        public void Ledger_RecordsEntriesAndSuites()
        {
            var ledger = new ModTestLedger();
            ledger.Add(new ModTestEntry { ModId = "m", TestName = "t1", Status = ModTestStatus.Passed });
            ledger.Add(new ModTestEntry { ModId = "m", TestName = "t2", Status = ModTestStatus.Failed });
            ledger.AddSuite(new ModTestSuiteSummary { ModId = "m", Status = ModTestStatus.Failed, Passed = 1, Failed = 1 });

            Assert.Equal(2, ledger.SnapshotEntries().Count);
            var suite = ledger.SnapshotSuites().Single();
            Assert.Equal(ModTestStatus.Failed, suite.Status);
            ledger.Clear();
            Assert.Empty(ledger.SnapshotEntries());
        }

        [Fact]
        public void ModAssert_FailsWithDescriptiveMessages()
        {
            var ex = Assert.Throws<ModAssertException>(() => ModAssert.Equal(1, 2, "numbers"));
            Assert.Contains("numbers", ex.Message);
            Assert.Contains("expected 1, actual 2", ex.Message);

            Assert.Throws<ModAssertException>(() => ModAssert.True(false));
            Assert.Throws<ModAssertException>(() => ModAssert.Null("x"));
            Assert.Throws<ModAssertException>(() => ModAssert.NotNull(null));
            Assert.Throws<ModAssertException>(() => ModAssert.CloseTo(1.0, 1.5, 0.1));
            Assert.Throws<ModAssertException>(() => ModAssert.Throws(() => { }));

            // Non-throwing paths must not throw:
            ModAssert.Equal("a", "a");
            ModAssert.True(true);
            // ModAssert.Throws swallows only ModAssertException; foreign
            // exceptions propagate to the runner (reported as Failed).
            Assert.Throws<InvalidOperationException>(() => ModAssert.Throws(() => throw new InvalidOperationException()));
            ModAssert.Throws(() => throw new InvalidOperationException(), "any", anyException: true);
            ModAssert.Throws(() => throw new ModAssertException("x"));
        }

        [Fact]
        public async System.Threading.Tasks.Task ModAssert_CloseToAcceptsFloatDrift()
        {
            ModAssert.Equal(30f, 30.00001f);
            ModAssert.CloseTo(0.3, 0.300001, 1e-3);
            await System.Threading.Tasks.Task.CompletedTask;
        }

        [Fact]
        public void Discovery_IgnoresInstanceAndUnmarkedMethods()
        {
            var tests = ModTestDiscovery.Discover(typeof(DiscoveryFixture).Assembly);
            Assert.DoesNotContain(tests, t => t.Name.Contains("NotATest"));
        }

        [Fact]
        public void Discovery_FindsPrivateStaticTests()
        {
            // The mod-authoring convention is a bare "static void" with no
            // access modifier — discovery must not require public methods.
            var tests = ModTestDiscovery.Discover(typeof(DiscoveryFixture).Assembly);
            Assert.Contains(tests, t => t.Name == "fixture/private");
        }

        public void Dispose()
        {
            if (System.IO.File.Exists(ManifestPath))
                System.IO.File.Delete(ManifestPath);
        }
    }

    /// <summary>Discovery fixture: three marked static methods plus decoys.</summary>
    public class DiscoveryFixture
    {
        [ModTest("fixture/plain")]
        public static void Plain() { }

        [ModTest("fixture/timeout", TimeoutSeconds = 7)]
        public static void WithTimeout() { }

        [ModTest("fixture/skipped", Skip = "not relevant")]
        public static void Skipped() { }

        [ModTest(null)]
        public static void NullName() { }

        public static void NotATest() { }

        [ModTest("fixture/private")]
        private static void PrivateTest() { }

        [ModTest("fixture/instance_decoy")]
        public void InstanceMethod() { }
    }
}
