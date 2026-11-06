using System;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>Pause, resume and cancel of one download as the UI drives them.</summary>
    public class DownloadControlTests
    {
        private static async Task<bool> Within(Task task, int milliseconds = 2000) =>
            await Task.WhenAny(task, Task.Delay(milliseconds)) == task;

        [Fact]
        public void PauseCancelsTheAttemptAndGivesAFreshOne()
        {
            var control = new DownloadControl();
            var attempt = control.AttemptToken;
            Assert.False(attempt.IsCancellationRequested);

            Assert.True(control.Pause());
            Assert.True(attempt.IsCancellationRequested);
            Assert.True(control.IsPaused);
            Assert.False(control.AttemptToken.IsCancellationRequested);
            Assert.NotEqual(attempt, control.AttemptToken);

            Assert.False(control.Pause());
        }

        [Fact]
        public async Task WaitWhilePausedStaysPendingUntilResume()
        {
            var control = new DownloadControl();
            control.Pause();
            var waiting = control.WaitWhilePausedAsync();
            await Task.Delay(100);
            Assert.False(waiting.IsCompleted);

            Assert.True(control.Resume());
            Assert.True(await Within(waiting));
            await waiting;
            Assert.False(control.IsPaused);
        }

        [Fact]
        public async Task CancelWhilePausedEndsTheWaitWithCancellation()
        {
            var control = new DownloadControl();
            control.Pause();
            var waiting = control.WaitWhilePausedAsync();

            control.Cancel();
            Assert.True(await Within(waiting));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.True(control.Cancellation.IsCancellationRequested);
            Assert.True(control.IsCancelled);
            Assert.False(control.Pause());
        }
    }
}
