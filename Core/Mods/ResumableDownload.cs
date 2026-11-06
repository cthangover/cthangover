using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One mod archive download over an unstable network. A broken or stalled
    /// transfer continues with <c>Range</c> (and <c>If-Range</c> once the server
    /// named its bytes) from what the file already holds, rotating mirrors and
    /// waiting per <see cref="TransferRetry"/>. When every mirror refuses (an
    /// expired private link) fresh links are requested; a server that ignores
    /// Range restarts the file. Slowness alone never aborts: only
    /// <see cref="TransferRetry.StallTimeout"/> without a byte restarts the attempt.
    /// The caller verifies the archive hash afterwards.
    /// </summary>
    public static class ResumableDownload
    {
        /// <summary>
        /// Downloads into <paramref name="path"/>, continuing what is already there.
        /// Returns false only after <see cref="TransferRetry.TransferFailures"/>
        /// attempts in a row moved no byte, or every mirror refused the file and
        /// fresh links could not be obtained.
        /// </summary>
        /// <param name="client">Client without an overall timeout; each read has its own stall timer.</param>
        /// <param name="download">Release to fetch; its size bounds a stale partial file.</param>
        /// <param name="path">Partial file to continue, or the destination of a new download.</param>
        /// <param name="renewUrls">Returns fresh links to the same bytes, or null when there are none.</param>
        /// <param name="progress">Received bytes, total (or -1) and the current retry streak (0 while it works).</param>
        /// <param name="control">Pause and cancel from the UI; null for neither. A cancel throws <see cref="OperationCanceledException"/>.</param>
        public static async Task<bool> RunAsync(HttpClient client, CatalogEntryDownload download, string path,
            Func<Task<List<string>>> renewUrls, Action<long, long, int> progress, DownloadControl control = null)
        {
            var cancellation = control?.Cancellation ?? CancellationToken.None;
            var retry = new TransferRetry();
            var urls = Usable(download.Urls);
            var mirror = 0;
            var refusals = 0;
            // ETag/Last-Modified of the bytes already on disk, for If-Range.
            RangeConditionHeaderValue validator = null;
            while (urls.Count > 0)
            {
                if (control != null)
                    await control.WaitWhilePausedAsync();
                var url = urls[mirror % urls.Count];
                var offset = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (download.SizeBytes > 0 && offset > download.SizeBytes)
                {
                    File.Delete(path);
                    offset = 0;
                }
                if (download.SizeBytes > 0 && offset == download.SizeBytes)
                    return true;
                TimeSpan? retryAfter = null;
                try
                {
                    // The stall timer, a pause (which drops the connection) and a cancel all end this attempt.
                    using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellation, control?.AttemptToken ?? default);
                    stall.CancelAfter(TransferRetry.StallTimeout);
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    if (offset > 0)
                    {
                        request.Headers.Range = new RangeHeaderValue(offset, null);
                        if (validator != null)
                            request.Headers.IfRange = validator;
                    }
                    using var response = await client.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, stall.Token);
                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && offset > 0)
                    {
                        // The partial already holds at least the whole file; the
                        // installer's hash check decides whether it is the right one.
                        if (response.Content.Headers.ContentRange?.Length == offset)
                            return true;
                        File.Delete(path);
                        validator = null;
                        continue;
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        retryAfter = TransferRetry.RetryAfter(response);
                        if (!TransferRetry.IsTransient(response.StatusCode) && ++refusals >= urls.Count)
                        {
                            // Every mirror refused: an expired private link or a
                            // removed file. Fresh links are the only way forward.
                            refusals = 0;
                            var renewed = Usable(await renewUrls());
                            if (renewed.Count == 0 || renewed.SequenceEqual(urls))
                            {
                                GameLogger.Log("DOWNLOADER", $"Every mirror refused the package (HTTP {(int)response.StatusCode})", LogLevel.Warning);
                                return false;
                            }
                            urls = renewed;
                        }
                        throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
                    }
                    refusals = 0;
                    var resumed = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent
                        && response.Content.Headers.ContentRange?.From == offset;
                    if (!resumed)
                        offset = 0;
                    validator = response.Headers.ETag is { IsWeak: false } etag
                        ? new RangeConditionHeaderValue(etag)
                        : response.Content.Headers.LastModified is { } modified ? new RangeConditionHeaderValue(modified) : null;
                    var total = resumed ? response.Content.Headers.ContentRange?.Length ?? -1
                        : response.Content.Headers.ContentLength ?? -1;
                    if (total <= 0 && download.SizeBytes > 0)
                        total = download.SizeBytes;
                    if (resumed)
                        GameLogger.Log("DOWNLOADER", $"Resuming package download at {offset} bytes");
                    else
                        GameLogger.Log("DOWNLOADER", "Downloading package...");

                    using var stream = await response.Content.ReadAsStreamAsync(stall.Token);
                    using var fileStream = new FileStream(path, resumed ? FileMode.Append : FileMode.Create, System.IO.FileAccess.Write);
                    var buffer = new byte[81920];
                    int bytesRead;
                    stall.CancelAfter(TransferRetry.StallTimeout);
                    while ((bytesRead = await stream.ReadAsync(buffer, stall.Token)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        stall.CancelAfter(TransferRetry.StallTimeout);
                        offset += bytesRead;
                        retry.Progressed();
                        progress?.Invoke(offset, total, 0);
                    }
                    if (total > 0 && offset < total)
                        throw new IOException("Connection closed before the end of the package");
                    GameLogger.Log("DOWNLOADER", $"Downloaded {offset} bytes");
                    return true;
                }
                catch (Exception ex) when (TransferRetry.IsTransient(ex, cancellation))
                {
                    // A pause is not a failure: wait for Resume, then continue at the offset.
                    if (control?.IsPaused == true)
                        continue;
                    // Temporary download URLs are secrets. HTTP exceptions can
                    // contain the full request URI, so log only the failure type.
                    GameLogger.Log("DOWNLOADER",
                        $"Package download interrupted: {ex.GetType().Name}; attempt {retry.Failures + 1}", LogLevel.Warning);
                }
                if (urls.Count > 1)
                    mirror++;
                progress?.Invoke(offset, download.SizeBytes > 0 ? download.SizeBytes : -1, retry.Failures + 1);
                if (!await retry.WaitAsync(retryAfter, cancellation))
                    return false;
            }
            return false;
        }

        private static List<string> Usable(List<string> urls) =>
            urls?.Where(url => !string.IsNullOrWhiteSpace(url)).ToList() ?? new List<string>();
    }
}
