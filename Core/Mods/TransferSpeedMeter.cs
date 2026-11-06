using System;
using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Current transfer speed over the last few seconds, so the UI shows what the
    /// connection does now rather than the average since the start. A smaller
    /// byte count than the previous sample (a restarted file) starts over.
    /// </summary>
    public sealed class TransferSpeedMeter
    {
        private readonly TimeSpan _window;
        private readonly Queue<(TimeSpan At, long Bytes)> _samples = new();
        private long _lastBytes = -1;

        /// <param name="window">Averaging window; five seconds by default.</param>
        public TransferSpeedMeter(TimeSpan? window = null)
        {
            _window = window ?? TimeSpan.FromSeconds(5);
        }

        /// <summary>Adds the total received at a moment; returns bytes per second over the window.</summary>
        public double Add(TimeSpan at, long bytes)
        {
            if (bytes < _lastBytes)
                _samples.Clear();
            _lastBytes = bytes;
            _samples.Enqueue((at, bytes));
            while (_samples.Count > 2 && at - _samples.Peek().At > _window)
                _samples.Dequeue();
            var first = _samples.Peek();
            var seconds = (at - first.At).TotalSeconds;
            return seconds > 0 ? (bytes - first.Bytes) / seconds : 0;
        }

        /// <summary>Forgets the samples, e.g. after a pause or a lost connection.</summary>
        public void Reset()
        {
            _samples.Clear();
            _lastBytes = -1;
        }
    }
}
