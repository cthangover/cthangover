using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        public void ModAssert_ComparesValuesWithUsefulDiagnostics()
        {
            ModAssert.Equal(true, true, "The opt-in flag survives reading the saved settings");
            ModAssert.Equal(DayOfWeek.Monday, DayOfWeek.Monday, "The weekly schedule starts on Monday");
            ModAssert.NotEqual(DayOfWeek.Sunday, DayOfWeek.Monday, "Weekday tasks are excluded from the weekend slot");
            ModAssert.Equal(.25, .26, .02, "Sampled travel uses quantized physics ticks");
            var unequal = Assert.Throws<ModAssertException>(() => ModAssert.Equal(true, false,
                "The opt-in flag survives reading the saved settings"));
            Assert.True(unequal.Message.Contains("expected true, actual false", StringComparison.Ordinal),
                "The report identifies the two states without requiring the reader to open the fixture");
            Assert.Throws<ModAssertException>(() => ModAssert.Equal(1f, float.NaN,
                "A corrupt height sample cannot match the authored elevation"));
        }

        [Fact]
        public void ModAssert_ReportsTheFirstSequenceDifference()
        {
            ModAssert.SequenceEqual(new[] { "Alpha", "Beta" }, new List<string> { "ALPHA", "BETA" },
                "Declared route aliases are case-insensitive and preserve ordering", StringComparer.OrdinalIgnoreCase);
            var wrong = Assert.Throws<ModAssertException>(() => ModAssert.SequenceEqual(new[] { 2, 4 }, new[] { 2, 5 },
                "Waypoints appear in the order prescribed by the route"));
            Assert.True(wrong.Message.Contains("index 1: expected 4, actual 5", StringComparison.Ordinal),
                "The mismatch gives a waypoint index and both identifiers");
            var shortRoute = Assert.Throws<ModAssertException>(() => ModAssert.SequenceEqual(new[] { 2, 4 }, new[] { 2 },
                "The route must retain its final waypoint"));
            Assert.True(shortRoute.Message.Contains("sequence ended at index 1", StringComparison.Ordinal),
                "A truncated route identifies which waypoint must be restored");
        }

        [Fact]
        public void ModAssert_DistinguishesReferenceIdentityAndRuntimeType()
        {
            var first = new List<int> { 1 };
            var second = new List<int> { 1 };
            ModAssert.Same(first, first, "The shared owner is reused by both consumers");
            ModAssert.NotSame(first, second, "Editing a copy must not modify the registered owner");
            ModAssert.IsType<List<int>>(first, "The builder supplies a concrete, editable list");
            ModAssert.IsAssignableFrom<IReadOnlyList<int>>(first, "Readers need indexed access to the registered values");
            Assert.Throws<ModAssertException>(() => ModAssert.IsType<IReadOnlyList<int>>(first,
                "An interface contract does not identify the underlying collection implementation"));
        }

        [Fact]
        public async Task ModAssert_ChecksSyncAndAsyncExceptionsWithContext()
        {
            var original = new ArgumentNullException("path");
            var failure = ModAssert.Throws<ArgumentException>(() => throw original,
                "Validation rejects the missing path before accessing the filesystem");
            ModAssert.Same(original, failure, "The caller can read the original parameter name");
            var asyncFailure = await ModAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(original),
                "Validation reports a missing input through the asynchronous entry point");
            ModAssert.Equal("path", asyncFailure.ParamName, "The diagnostic names the configuration field to correct");
            var wrong = Assert.Throws<ModAssertException>(() => ModAssert.Throws<InvalidOperationException>(() => throw original,
                "Wrong state and missing configuration have different remediation paths"));
            ModAssert.Same(original, wrong.InnerException, "The diagnostic preserves the unexpected cause and stack");
            await ModAssert.DoesNotThrowAsync(() => Task.CompletedTask, "Closing an idle operation is valid");
            Assert.Throws<OperationCanceledException>(() => ModAssert.DoesNotThrow(() => throw new OperationCanceledException(),
                "Cancellation must be available to the test runner"));
        }

        [Fact]
        public void ModAssert_AllCollectsIndependentFailuresWithTheirContext()
        {
            var failure = Assert.Throws<ModAssertException>(() => ModAssert.All("Restored actor state",
                () => ModAssert.True(false, "The saved actor must remain grounded after restoring the location"),
                () => ModAssert.Fail("The restore never committed the authored support identifier")));
            Assert.True(failure.Message.Contains("2 assertions failed", StringComparison.Ordinal)
                && failure.Message.Contains("saved actor", StringComparison.Ordinal)
                && failure.Message.Contains("support identifier", StringComparison.Ordinal),
                "The grouped result surfaces both independent restore problems in one report");
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

        public static void NullName() { }

        public static void NotATest() { }

        [ModTest("fixture/private")]
        private static void PrivateTest() { }

        public void InstanceMethod() { }
    }
}
