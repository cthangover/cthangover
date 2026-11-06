using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>FIFO turns of the download queue: the first turn is free, waiters keep their order, cancelled waiters leave.</summary>
    public class DownloadQueueTests
    {
        private static async Task<bool> Within(Task task, int milliseconds = 2000) =>
            await Task.WhenAny(task, Task.Delay(milliseconds)) == task;

        private static async Task Take(DownloadQueue queue, int id, List<int> order)
        {
            using var turn = await queue.EnterAsync(CancellationToken.None);
            lock (order)
                order.Add(id);
        }

        [Fact]
        public async Task FirstTurnIsImmediateAndSecondWaitsUntilItIsDisposed()
        {
            var queue = new DownloadQueue();
            var first = queue.EnterAsync(CancellationToken.None);
            Assert.True(first.IsCompleted);
            Assert.Equal(0, queue.Waiting);

            var second = queue.EnterAsync(CancellationToken.None);
            await Task.Delay(50);
            Assert.False(second.IsCompleted);
            Assert.Equal(1, queue.Waiting);

            (await first).Dispose();
            Assert.True(await Within(second));
            (await second).Dispose();
            Assert.Equal(0, queue.Waiting);
        }

        [Fact]
        public async Task WaitersGetTheirTurnsInArrivalOrder()
        {
            var queue = new DownloadQueue();
            var order = new List<int>();
            var holder = await queue.EnterAsync(CancellationToken.None);

            // Each Take enters the queue synchronously before its first await, so the arrival order is 1, 2, 3.
            var waiters = new[] { Take(queue, 1, order), Take(queue, 2, order), Take(queue, 3, order) };
            Assert.Equal(3, queue.Waiting);

            holder.Dispose();
            Assert.True(await Within(Task.WhenAll(waiters)));
            await Task.WhenAll(waiters);
            Assert.Equal(new[] { 1, 2, 3 }, order);
        }

        [Fact]
        public async Task CancelledWaiterThrowsAndIsSkipped()
        {
            var queue = new DownloadQueue();
            var holder = await queue.EnterAsync(CancellationToken.None);
            using var cancel = new CancellationTokenSource();
            var cancelled = queue.EnterAsync(cancel.Token);
            var next = queue.EnterAsync(CancellationToken.None);
            Assert.Equal(2, queue.Waiting);

            cancel.Cancel();
            Assert.True(await Within(cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.Equal(1, queue.Waiting);

            holder.Dispose();
            Assert.True(await Within(next));
            (await next).Dispose();
        }

        [Fact]
        public async Task DisposingATurnTwiceDoesNotHandOutTwoTurns()
        {
            var queue = new DownloadQueue();
            var holder = await queue.EnterAsync(CancellationToken.None);
            var a = queue.EnterAsync(CancellationToken.None);
            var b = queue.EnterAsync(CancellationToken.None);

            holder.Dispose();
            holder.Dispose();
            Assert.True(await Within(a));
            await Task.Delay(50);
            Assert.False(b.IsCompleted);
            Assert.Equal(1, queue.Waiting);

            (await a).Dispose();
            Assert.True(await Within(b));
            (await b).Dispose();
        }
    }
}
