using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>Retry policy shared by catalog requests, downloads and publishing.</summary>
    public class TransferRetryTests
    {
        [Fact]
        public async Task ProgressStartsANewFailureStreak()
        {
            var retry = new TransferRetry(2);
            Assert.True(await retry.WaitAsync(TimeSpan.Zero, CancellationToken.None));
            retry.Progressed();
            Assert.True(await retry.WaitAsync(TimeSpan.Zero, CancellationToken.None));
            Assert.False(await retry.WaitAsync(TimeSpan.Zero, CancellationToken.None));
        }

        [Fact]
        public async Task ALongServerPauseEndsRetrying()
        {
            var retry = new TransferRetry();
            Assert.False(await retry.WaitAsync(TimeSpan.FromDays(7), CancellationToken.None));
        }

        [Fact]
        public void OnlyChangeableAnswersAndNetworkFailuresAreTransient()
        {
            Assert.True(TransferRetry.IsTransient(HttpStatusCode.ServiceUnavailable));
            Assert.True(TransferRetry.IsTransient(HttpStatusCode.TooManyRequests));
            Assert.False(TransferRetry.IsTransient(HttpStatusCode.NotFound));
            Assert.False(TransferRetry.IsTransient(HttpStatusCode.Forbidden));
            Assert.True(TransferRetry.IsTransient(new IOException(), CancellationToken.None));
            Assert.True(TransferRetry.IsTransient(new TaskCanceledException(), CancellationToken.None));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.False(TransferRetry.IsTransient(new TaskCanceledException(), cancelled.Token));
            Assert.False(TransferRetry.IsTransient(new InvalidDataException(), CancellationToken.None));
        }

        [Fact]
        public void RetryAfterIsReadFromTheAnswer()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            Assert.Null(TransferRetry.RetryAfter(response));
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            Assert.Equal(TimeSpan.FromSeconds(10), TransferRetry.RetryAfter(response));
        }
    }
}
