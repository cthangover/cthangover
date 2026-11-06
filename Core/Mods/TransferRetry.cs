using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Retry policy for network work over unstable connections: catalog requests,
    /// mod downloads and publishing. Interruptions are normal operation, so a
    /// transfer resumes from its offset and gives up only after
    /// <c>maxFailures</c> consecutive attempts that moved no byte; any progress
    /// (<see cref="Progressed"/>) resets the count. Delays grow exponentially up
    /// to 30 s with jitter, or follow the server's <c>Retry-After</c>. A
    /// <c>Retry-After</c> longer than five minutes (an IP ban) ends retrying.
    /// </summary>
    public sealed class TransferRetry
    {
        /// <summary>Failures in a row a download or upload tolerates: about nine minutes without any byte.</summary>
        public const int TransferFailures = 20;

        /// <summary>How long a transfer may receive or send nothing before the attempt is restarted.</summary>
        public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

        private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxServerDelay = TimeSpan.FromMinutes(5);
        private readonly int _maxFailures;

        /// <param name="maxFailures">Consecutive failed attempts before giving up.</param>
        public TransferRetry(int maxFailures = TransferFailures)
        {
            _maxFailures = maxFailures;
        }

        /// <summary>Consecutive failed attempts since the last progress.</summary>
        public int Failures { get; private set; }

        /// <summary>Marks progress: the next failure starts a new streak.</summary>
        public void Progressed() => Failures = 0;

        /// <summary>
        /// Counts a failed attempt and waits before the next one. Returns
        /// <c>false</c> when the transfer should give up instead.
        /// </summary>
        public async Task<bool> WaitAsync(TimeSpan? retryAfter, CancellationToken cancellation)
        {
            Failures++;
            if (Failures >= _maxFailures)
                return false;
            var delay = retryAfter ?? TimeSpan.FromSeconds(Math.Min(MaxDelay.TotalSeconds, Math.Pow(2, Failures - 1)));
            if (delay > MaxServerDelay)
                return false;
            await Task.Delay(delay + TimeSpan.FromMilliseconds(Random.Shared.Next(250, 1000)), cancellation);
            return true;
        }

        /// <summary>The server's requested pause, or null when it gave none.</summary>
        public static TimeSpan? RetryAfter(HttpResponseMessage response)
        {
            var header = response?.Headers.RetryAfter;
            if (header?.Delta is { } delta)
                return delta;
            if (header?.Date is { } date)
                return date > DateTimeOffset.UtcNow ? date - DateTimeOffset.UtcNow : TimeSpan.Zero;
            return null;
        }

        /// <summary>True for answers a later attempt can change: timeouts, overload and gateway errors.</summary>
        public static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

        /// <summary>
        /// True for network failures, including an attempt cancelled by the stall
        /// timer, but never for the caller's own cancellation.
        /// </summary>
        public static bool IsTransient(Exception error, CancellationToken cancellation) => !cancellation.IsCancellationRequested
            && error is HttpRequestException or IOException or OperationCanceledException;
    }
}
