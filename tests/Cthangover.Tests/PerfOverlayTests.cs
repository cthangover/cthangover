using System;
using System.Text.Json;
using Cthangover.Core.Mods.Testing;
using Cthangover.Mods.PerformanceOverlay;
using Xunit;

namespace Cthangover.Tests
{
    /// <summary>
    /// Pure managed contracts for frame-window accounting and truthful test identity; no Godot lifecycle or
    /// graphics APIs are invoked.
    /// </summary>
    public sealed class PerfOverlayTests
    {
        private PerfFrameWindow invalidWindow;
        private double invalidSeconds;
        private double invalidMilliseconds;
        private void AddInvalidSample()
        {
            invalidWindow.Add(invalidSeconds, invalidMilliseconds);
        }

        private void InvalidFirstIndex()
        {
            new ModTestProgress("fixture", "bad", 0, 7, 0);
        }

        private void InvalidLastIndex()
        {
            new ModTestProgress("fixture", "bad", 8, 7, 0);
        }

        /// <summary>
        /// Checks exact nearest-rank percentiles and expiry by sample wall time, including the empty-window
        /// reset.
        /// </summary>
        [Fact]
        public void FiveSecondWindowUsesWallTimeAndExactNearestRanks()
        {
            var window = new PerfFrameWindow();
            var result = new PerfFrameSummary();
            for (var i = 1; i <= 100; i++)
            {
                window.Add(i * .01, i);
            }

            window.Summarize(1, result);
            Assert.Equal(100, result.Count);
            Assert.Equal(50.5, result.MeanMs);
            Assert.Equal(95, result.P95Ms);
            Assert.Equal(99, result.P99Ms);
            Assert.Equal(100, result.MaxMs);
            window.Summarize(5.51, result);
            Assert.Equal(50, result.Count);
            Assert.Equal(100, result.CurrentMs);
            window.Summarize(7, result);
            Assert.Equal(0, result.Count);
            Assert.Equal(0, result.MaxMs);
        }

        /// <summary>
        /// Checks strict hitch boundaries and lifetime counters after all window samples have expired.
        /// </summary>
        [Fact]
        public void HitchThresholdsAreStrictAndCountsSurviveWindowExpiry()
        {
            var window = new PerfFrameWindow();
            var result = new PerfFrameSummary();
            window.Add(0, 50);
            window.Add(1, 50.001);
            window.Add(2, 250);
            window.Add(3, 250.001);
            window.Summarize(10, result);
            Assert.Equal(3, result.Over50);
            Assert.Equal(1, result.Over250);
            Assert.Equal(3, result.LastHitchSeconds);
            Assert.Equal(0, result.Count);
        }

        /// <summary>
        /// Checks bounded ring loss reporting, retained statistics and graph order without losing lifetime
        /// hitch counts.
        /// </summary>
        [Fact]
        public void RingOverflowIsExplicitAndDoesNotLoseHitchCounts()
        {
            var window = new PerfFrameWindow(3);
            var result = new PerfFrameSummary();
            window.Add(0, 300);
            window.Add(1, 10);
            window.Add(2, 20);
            window.Add(3, 30);
            window.Summarize(3, result);
            Assert.Equal(1, result.DroppedSamples);
            Assert.Equal(1, result.Over250);
            Assert.Equal(30, result.MaxMs);
            Assert.Equal(20, result.MeanMs);
            var graph = new double[2];
            Assert.Equal(2, window.Graph(graph));
            Assert.Equal(new double[]
            {
                20,
                30
            }, graph);
        }

        /// <summary>
        /// Checks negative and nonfinite sample rejection through the public collector contract.
        /// </summary>
        [Theory]
        [InlineData(-1, 1)]
        [InlineData(1, -1)]
        [InlineData(double.NaN, 1)]
        [InlineData(1, double.PositiveInfinity)]
        public void InvalidSamplesAreRejected(double seconds, double milliseconds)
        {
            invalidWindow = new PerfFrameWindow();
            invalidSeconds = seconds;
            invalidMilliseconds = milliseconds;
            Assert.Throws<ArgumentException>(AddInvalidSample);
        }

        /// <summary>
        /// Checks managed allocation count across repeated per-frame additions after construction.
        /// </summary>
        [Fact]
        public void PerFrameCollectionDoesNotAllocate()
        {
            var window = new PerfFrameWindow();
            window.Add(0, 1);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 1; i < 10000; i++)
            {
                window.Add(i * .001, 1);
            }

            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        }

        /// <summary>
        /// Checks a complete serialized summary round trip, comparing every persisted field through the
        /// resulting JSON.
        /// </summary>
        [Fact]
        public void SummaryRoundTripsEveryField()
        {
            var original = new PerfFrameSummary
            {
                Count = 3,
                CurrentMs = 9,
                MeanMs = 10,
                P95Ms = 12,
                P99Ms = 13,
                MaxMs = 14,
                Over50 = 7,
                Over250 = 2,
                LastHitchSeconds = 3.5,
                DroppedSamples = 4
            };
            var json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<PerfFrameSummary>(json);
            Assert.Equal(json, JsonSerializer.Serialize(restored));
        }

        /// <summary>
        /// Checks test-mode/suite parameter decoding and distinguishes ordinary launches from scene-driver
        /// runs.
        /// </summary>
        [Fact]
        public void ContextDoesNotInventModTestIdentityForSceneDrivers()
        {
            var context = new PerfRunContext(new[]
            {
                "--itest=perf_overlay_contracts",
                "--quality=high"
            });
            Assert.True(context.IsTest);
            Assert.Equal("contract", context.Mode);
            Assert.Equal("perf_overlay_contracts", context.Suite);
            Assert.True(new PerfRunContext(new[] { "--choices=0,1" }).IsTest);
            Assert.Equal("matrix", new PerfRunContext(new[] { "--matrix=terrain" }).Mode);
            Assert.False(new PerfRunContext(Array.Empty<string>()).IsTest);
            Assert.Equal("unit", new PerfRunContext(new[]
            {
                "--run-tests",
                "--mod=fixture"
            }).Mode);
        }

        /// <summary>
        /// Checks exact case identity/counts, nonnegative elapsed wall time and rejection of invalid one-based
        /// indices without publication.
        /// </summary>
        [Fact]
        public void ReadOnlyModTestProgressReportsIdentityAndMonotonicElapsed()
        {
            var progress = new ModTestProgress("fixture", "case/precise", 2, 7, 12);
            Assert.Equal("fixture", progress.ModId);
            Assert.Equal("case/precise", progress.TestId);
            Assert.Equal(2, progress.Index);
            Assert.Equal(7, progress.Total);
            Assert.True(progress.CaseSeconds >= 0);
            Assert.True(progress.SuiteSeconds >= 12);
            Assert.Throws<ArgumentException>(InvalidFirstIndex);
            Assert.Throws<ArgumentException>(InvalidLastIndex);
            Assert.Null(ModTestProgress.Current);
        }
    }
}
