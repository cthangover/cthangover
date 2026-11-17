namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.TestRunner;

    /// <summary>Stops at an acknowledged preparation boundary; tests release or cancel it explicitly instead of racing a sleep.</summary>
    internal sealed class PreparationGate : IPreparedInputObserver, IDisposable
    {
        private readonly string phase;
        private readonly CancellationToken cancellationToken;
        private readonly ManualResetEventSlim released = new();
        private readonly TaskCompletionSource<bool> reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Ready => reached.Task.WaitAsync(TimeSpan.FromSeconds(20));

        internal bool Fail { get; set; }

        internal PreparationGate(string phase, CancellationToken cancellationToken)
        {
            this.phase = phase;
            this.cancellationToken = cancellationToken;
        }

        /// <summary>Signals only the requested synchronous store boundary and never retains a callback after preparation.</summary>
        public void Observe(string observedPhase, string root, string sourceFingerprint)
        {
            if (observedPhase != phase)
            {
                return;
            }

            reached.TrySetResult(true);
            if (!released.Wait(TimeSpan.FromSeconds(20), cancellationToken))
            {
                throw new TimeoutException("Preparation gate was not explicitly released.");
            }

            if (Fail)
            {
                throw new IOException("Owned preparation observer failure.");
            }
        }

        internal void Release()
        {
            released.Set();
        }

        /// <summary>Call only after the worker has joined; disposal does not abandon a running preparation.</summary>
        public void Dispose()
        {
            released.Dispose();
        }
    }
}
