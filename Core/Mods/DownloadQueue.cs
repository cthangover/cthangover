using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// First-in, first-out turn for downloads: one archive at a time, so a player
    /// holds one connection to a server and several mods arrive in the order the
    /// player chose them. A cancelled waiter leaves the queue without a turn.
    /// </summary>
    public sealed class DownloadQueue
    {
        private readonly object _lock = new();
        private readonly LinkedList<TaskCompletionSource<IDisposable>> _waiting = new();
        private bool _busy;

        /// <summary>Number of downloads waiting for their turn.</summary>
        public int Waiting
        {
            get { lock (_lock) return _waiting.Count; }
        }

        /// <summary>Waits for the turn; disposing the result passes it to the next waiter.</summary>
        public async Task<IDisposable> EnterAsync(CancellationToken cancellation)
        {
            TaskCompletionSource<IDisposable> turn;
            LinkedListNode<TaskCompletionSource<IDisposable>> node;
            lock (_lock)
            {
                if (!_busy)
                {
                    _busy = true;
                    return new Turn(this);
                }
                turn = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
                node = _waiting.AddLast(turn);
            }
            using (cancellation.Register(() =>
            {
                lock (_lock)
                {
                    if (node.List == null)
                        return;
                    _waiting.Remove(node);
                }
                turn.TrySetCanceled(cancellation);
            }))
            {
                return await turn.Task;
            }
        }

        private void Leave()
        {
            TaskCompletionSource<IDisposable> next;
            lock (_lock)
            {
                if (_waiting.Count == 0)
                {
                    _busy = false;
                    return;
                }
                next = _waiting.First.Value;
                _waiting.RemoveFirst();
            }
            next.SetResult(new Turn(this));
        }

        private sealed class Turn : IDisposable
        {
            private DownloadQueue _queue;

            public Turn(DownloadQueue queue) => _queue = queue;

            public void Dispose() => Interlocked.Exchange(ref _queue, null)?.Leave();
        }
    }
}
