using System;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>Speed over the recent window: steady rates, forgotten slow samples and restarted files.</summary>
    public class TransferSpeedMeterTests
    {
        private const long MiB = 1024 * 1024;

        [Fact]
        public void FirstSampleGivesZero()
        {
            var meter = new TransferSpeedMeter();
            Assert.Equal(0.0, meter.Add(TimeSpan.FromSeconds(5), 123_456));
        }

        [Fact]
        public void SteadyMebibytePerSecondIsReported()
        {
            var meter = new TransferSpeedMeter();
            var speed = 0.0;
            for (var second = 0; second <= 3; second++)
                speed = meter.Add(TimeSpan.FromSeconds(second), second * MiB);
            Assert.InRange(speed, MiB * 0.99, MiB * 1.01);
        }

        [Fact]
        public void OnlyTheLastWindowCounts()
        {
            var meter = new TransferSpeedMeter(TimeSpan.FromSeconds(2));
            for (var second = 0; second <= 10; second++)
                meter.Add(TimeSpan.FromSeconds(second), second * 1024L);

            var speed = 0.0;
            for (var second = 11; second <= 20; second++)
                speed = meter.Add(TimeSpan.FromSeconds(second), 10 * 1024L + (second - 10) * MiB);
            Assert.InRange(speed, MiB * 0.99, MiB * 1.01);
        }

        [Fact]
        public void SmallerByteCountStartsOver()
        {
            var meter = new TransferSpeedMeter();
            meter.Add(TimeSpan.Zero, 0);
            meter.Add(TimeSpan.FromSeconds(1), 5 * MiB);

            var restarted = meter.Add(TimeSpan.FromSeconds(2), 1000);
            Assert.Equal(0.0, restarted);

            var speed = meter.Add(TimeSpan.FromSeconds(3), 1000 + MiB);
            Assert.InRange(speed, MiB * 0.99, MiB * 1.01);
        }
    }
}
