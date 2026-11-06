using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Downloads over a local server that breaks connections the way unstable
    /// networks do: the client must resume with Range, renew refused links and
    /// restart when a server ignores Range.
    /// </summary>
    public class ResumableDownloadTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "resumable-" + Guid.NewGuid().ToString("N"));
        private readonly HttpListener listener = new();
        private readonly HttpClient client = new(new HttpClientHandler { UseProxy = false });
        private readonly byte[] payload = new byte[512 * 1024];
        private readonly ConcurrentQueue<string> requests = new();
        private readonly string origin;
        private Func<HttpListenerContext, int, Task> serve;

        public ResumableDownloadTests()
        {
            Directory.CreateDirectory(root);
            new Random(7).NextBytes(payload);
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            origin = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(origin);
            listener.Start();
            _ = Task.Run(Loop);
        }

        private async Task Loop()
        {
            var count = 0;
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); }
                catch (Exception) { return; }
                requests.Enqueue(context.Request.Url!.AbsolutePath + " " + (context.Request.Headers["Range"] ?? "-"));
                try { await serve(context, ++count); }
                catch (Exception) { context.Response.Abort(); }
            }
        }

        private async Task Full(HttpListenerContext context)
        {
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload);
            context.Response.Close();
        }

        private async Task Rest(HttpListenerContext context)
        {
            var from = long.Parse(context.Request.Headers["Range"]!.Split('=')[1].TrimEnd('-'));
            context.Response.StatusCode = 206;
            context.Response.Headers["Content-Range"] = $"bytes {from}-{payload.Length - 1}/{payload.Length}";
            context.Response.ContentLength64 = payload.Length - from;
            await context.Response.OutputStream.WriteAsync(payload.AsMemory((int)from));
            context.Response.Close();
        }

        private CatalogEntryDownload Download(params string[] paths)
        {
            var urls = new List<string>();
            foreach (var path in paths)
                urls.Add(origin + path);
            return new CatalogEntryDownload { Version = "1", Urls = urls, SizeBytes = payload.Length };
        }

        [Fact]
        public async Task BrokenConnectionResumesWithRange()
        {
            serve = async (context, n) =>
            {
                if (n > 1)
                {
                    await Rest(context);
                    return;
                }
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload.AsMemory(0, 200 * 1024));
                await context.Response.OutputStream.FlushAsync();
                context.Response.Abort();
            };
            var path = Path.Combine(root, "a.part");
            Assert.True(await ResumableDownload.RunAsync(client, Download("mod.zip"), path, () => Task.FromResult<List<string>>(null), null));
            Assert.Equal(payload, File.ReadAllBytes(path));
            Assert.Contains(requests, r => r.StartsWith("/mod.zip bytes=") && !r.EndsWith("bytes=0-"));
        }

        [Fact]
        public async Task RefusedLinksAreRenewedAndThePartialIsKept()
        {
            serve = (context, n) =>
            {
                if (context.Request.Url!.AbsolutePath == "/expired")
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return Task.CompletedTask;
                }
                return Rest(context);
            };
            var path = Path.Combine(root, "b.part");
            File.WriteAllBytes(path, payload.AsSpan(0, 100_000).ToArray());
            var renewals = 0;
            Assert.True(await ResumableDownload.RunAsync(client, Download("expired"), path, () =>
            {
                renewals++;
                return Task.FromResult(new List<string> { origin + "fresh" });
            }, null));
            Assert.Equal(1, renewals);
            Assert.Equal(payload, File.ReadAllBytes(path));
            Assert.Contains("/fresh bytes=100000-", requests);
        }

        [Fact]
        public async Task ServerIgnoringRangeRestartsFromTheBeginning()
        {
            serve = (context, n) => Full(context);
            var path = Path.Combine(root, "c.part");
            File.WriteAllBytes(path, new byte[1000]);
            Assert.True(await ResumableDownload.RunAsync(client, Download("mod.zip"), path, () => Task.FromResult<List<string>>(null), null));
            Assert.Equal(payload, File.ReadAllBytes(path));
        }

        [Fact]
        public async Task EveryMirrorRefusingWithoutFreshLinksFails()
        {
            serve = (context, n) =>
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return Task.CompletedTask;
            };
            var path = Path.Combine(root, "d.part");
            Assert.False(await ResumableDownload.RunAsync(client, Download("gone"), path, () => Task.FromResult<List<string>>(null), null));
        }

        /// <summary>Writes the payload in small chunks with pauses, honouring Range, so a transfer lasts long enough to pause or cancel.</summary>
        private async Task Slow(HttpListenerContext context)
        {
            const int chunk = 16 * 1024;
            var from = 0;
            var range = context.Request.Headers["Range"];
            if (range != null)
            {
                from = (int)long.Parse(range.Split('=')[1].TrimEnd('-'));
                context.Response.StatusCode = 206;
                context.Response.Headers["Content-Range"] = $"bytes {from}-{payload.Length - 1}/{payload.Length}";
            }
            context.Response.ContentLength64 = payload.Length - from;
            for (var at = from; at < payload.Length; at += chunk)
            {
                var count = Math.Min(chunk, payload.Length - at);
                await context.Response.OutputStream.WriteAsync(payload.AsMemory(at, count));
                await context.Response.OutputStream.FlushAsync();
                await Task.Delay(20);
            }
            context.Response.Close();
        }

        private static async Task<bool> Within(Task task, int milliseconds = 4000) =>
            await Task.WhenAny(task, Task.Delay(milliseconds)) == task;

        [Fact]
        public async Task PauseDropsTheConnectionAndResumeContinuesWithRange()
        {
            serve = (context, n) => Slow(context);
            var path = Path.Combine(root, "pause.part");
            var control = new DownloadControl();
            var paused = new TaskCompletionSource();
            var pausedOnce = false;
            var task = ResumableDownload.RunAsync(client, Download("slow.zip"), path, () => Task.FromResult<List<string>>(null),
                (received, total, streak) =>
                {
                    if (pausedOnce || received <= 64 * 1024)
                        return;
                    pausedOnce = true;
                    control.Pause();
                    paused.TrySetResult();
                }, control);

            Assert.True(await Within(paused.Task));
            await Task.Delay(300);
            var length = new FileInfo(path).Length;
            await Task.Delay(200);
            Assert.Equal(length, new FileInfo(path).Length);

            control.Resume();
            Assert.True(await Within(task));
            Assert.True(await task);
            Assert.Equal(payload, File.ReadAllBytes(path));
            Assert.Contains(requests.ToArray().Skip(1), r => r.Contains("bytes=") && !r.Contains("bytes=0-"));
        }

        [Fact]
        public async Task CancelThrowsAndStopsTheTransfer()
        {
            serve = (context, n) => Slow(context);
            var path = Path.Combine(root, "cancel.part");
            var control = new DownloadControl();
            var cancelled = false;
            var task = ResumableDownload.RunAsync(client, Download("slow.zip"), path, () => Task.FromResult<List<string>>(null),
                (received, total, streak) =>
                {
                    if (cancelled)
                        return;
                    cancelled = true;
                    control.Cancel();
                }, control);

            Assert.True(await Within(task));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            var length = new FileInfo(path).Length;
            await Task.Delay(200);
            Assert.Equal(length, new FileInfo(path).Length);
        }

        public void Dispose()
        {
            listener.Stop();
            listener.Close();
            client.Dispose();
            Directory.Delete(root, true);
        }
    }
}
