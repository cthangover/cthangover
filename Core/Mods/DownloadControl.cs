using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Pause, resume and cancel of one mod download, driven by the UI. Pausing
    /// cancels the current connection (<see cref="AttemptToken"/>) so the server
    /// slot is released at once; the downloader keeps the partial file and
    /// continues with <c>Range</c> after <see cref="Resume"/>. Cancelling ends
    /// the install, also while it waits in the queue or is paused.
    /// </summary>
    public sealed class DownloadControl
    {
        private readonly object _lock = new();
        private readonly CancellationTokenSource _cancel = new();
        private CancellationTokenSource _attempt = new();
        private TaskCompletionSource _resumed;

        /// <summary>Cancelled when the player cancels the install.</summary>
        public CancellationToken Cancellation => _cancel.Token;

        /// <summary>True between <see cref="Pause"/> and <see cref="Resume"/>.</summary>
        public bool IsPaused
        {
            get { lock (_lock) return _resumed != null; }
        }

        /// <summary>True once <see cref="Cancel"/> was called.</summary>
        public bool IsCancelled => _cancel.IsCancellationRequested;

        /// <summary>Token of the current connection; a pause cancels it.</summary>
        public CancellationToken AttemptToken
        {
            get { lock (_lock) return _attempt.Token; }
        }

        /// <summary>Stops the transfer and keeps what was received. Returns false if already paused or cancelled.</summary>
        public bool Pause()
        {
            CancellationTokenSource attempt;
            lock (_lock)
            {
                if (_resumed != null || IsCancelled)
                    return false;
                _resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                attempt = _attempt;
                _attempt = new CancellationTokenSource();
            }
            attempt.Cancel();
            return true;
        }

        /// <summary>Continues a paused transfer. Returns false if it was not paused.</summary>
        public bool Resume()
        {
            TaskCompletionSource resumed;
            lock (_lock)
            {
                resumed = _resumed;
                _resumed = null;
            }
            resumed?.SetResult();
            return resumed != null;
        }

        /// <summary>Ends the install; a queued or paused one ends too.</summary>
        public void Cancel()
        {
            _cancel.Cancel();
            Resume();
        }

        /// <summary>Waits while paused; throws <see cref="OperationCanceledException"/> after <see cref="Cancel"/>.</summary>
        public async Task WaitWhilePausedAsync()
        {
            Task resumed;
            lock (_lock)
                resumed = _resumed?.Task;
            if (resumed != null)
                await resumed;
            _cancel.Token.ThrowIfCancellationRequested();
        }
    }
}
