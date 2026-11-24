namespace Cthangover.Core.Mods.Host.Transport.Impls
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Legacy same-user command transport. Each game instance has its own pipe, so clients can connect
    /// by PID. Protocol 1 exchanges one newline-terminated UTF-8 JSON request and response per connection.
    /// </summary>
    public sealed class DebugLocalPipeTransport : IDebugHostTransport
    {
        private const int MaxRequest = 64 * 1024;
        private const string DefaultName = "cthangover-dev";
        private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);

        private readonly string name;
        private readonly CancellationTokenSource stop = new();
        private readonly object lifecycle = new();
        private Task listener = Task.CompletedTask;
        private Task shutdown;

        /// <summary>Constructs an endpoint whose name is unique to this process.</summary>
        public DebugLocalPipeTransport(string pipeName = null)
        {
            var prefix = pipeName?.Trim();
            if (string.IsNullOrEmpty(prefix))
                prefix = DefaultName;
            else if (prefix.Contains('/') || prefix.Contains('\\'))
            {
                DebugHostInstance.Log("pipe name '" + prefix + "' must be a plain name; using " + DefaultName,
                    LogLevel.Warning);
                prefix = DefaultName;
            }

            name = prefix + "-" + System.Environment.ProcessId;
        }

        /// <summary>The label used for this transport in diagnostics.</summary>
        public string Name => "local-pipe";

        /// <summary>This transport exposes a same-user IPC endpoint.</summary>
        public bool IsLocal => true;

        /// <summary>Process-specific connection information published after the listener opens.</summary>
        public HostEndpointDescriptor Endpoint { get; private set; }

        /// <summary>Opens the process-specific pipe and serves client frames through the shared game queue.</summary>
        public void Start(DebugHostBus bus)
        {
            ArgumentNullException.ThrowIfNull(bus);
            lock (lifecycle)
            {
                if (Endpoint != null || shutdown != null)
                    throw new InvalidOperationException("The development pipe can be started once.");

                var pipe = CreatePipe();
                Endpoint = new HostEndpointDescriptor("pipe", name, 1);
                listener = ServeAsync(pipe, bus, stop.Token);
            }
        }

        /// <summary>Signals cancellation and waits for the listener to release the current connection.</summary>
        public Task StopAsync()
        {
            lock (lifecycle)
            {
                return shutdown ??= StopListenerAsync();
            }
        }

        /// <summary>Signals listener shutdown for callers that own no asynchronous cleanup barrier.</summary>
        public void Stop()
        {
            _ = StopAsync();
        }

        private async Task StopListenerAsync()
        {
            try
            {
                await stop.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await listener.ConfigureAwait(false);
                }
                finally
                {
                    stop.Dispose();
                }
            }
        }

        private NamedPipeServerStream CreatePipe() => new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        private async Task ServeAsync(NamedPipeServerStream firstPipe, DebugHostBus bus, CancellationToken token)
        {
            var pipe = firstPipe;
            try
            {
                while (pipe != null)
                {
                    using (pipe)
                    {
                        using var interrupt = token.Register(pipe.Dispose);
                        await ServeClientAsync(pipe, bus, token).ConfigureAwait(false);
                    }

                    pipe = await ReopenAsync(token).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (token.IsCancellationRequested
                && (error is OperationCanceledException or ObjectDisposedException or IOException))
            {
            }
            catch (Exception error)
            {
                DebugHostInstance.Log("development pipe stopped: " + error.Message, LogLevel.Warning);
            }
        }

        private async Task<NamedPipeServerStream> ReopenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    return CreatePipe();
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    DebugHostInstance.Log("development pipe unavailable: " + error.Message, LogLevel.Warning);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }

            return null;
        }

        private async Task ServeClientAsync(NamedPipeServerStream pipe, DebugHostBus bus, CancellationToken token)
        {
            long connection = 0;
            try
            {
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                connection = bus.OpenConnection(Name);
                var answer = await ReadAndDispatchAsync(pipe, bus, connection, token).ConfigureAwait(false);
                using var writeDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                writeDeadline.CancelAfter(IoTimeout);
                var bytes = Encoding.UTF8.GetBytes(answer.ToJsonString() + "\n");
                await pipe.WriteAsync(bytes, writeDeadline.Token).ConfigureAwait(false);
                await pipe.FlushAsync(writeDeadline.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (!token.IsCancellationRequested
                && (error is IOException or UnauthorizedAccessException or OperationCanceledException))
            {
                // A disconnected or unauthorized client must never stop the listener.
            }
            finally
            {
                if (connection != 0)
                    bus.CloseConnection(connection);
            }
        }

        private static async Task<JsonObject> ReadAndDispatchAsync(Stream pipe, DebugHostBus bus, long connection,
            CancellationToken token)
        {
            JsonObject request;
            using (var readDeadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                readDeadline.CancelAfter(IoTimeout);
                try
                {
                    request = await ReadRequestAsync(pipe, readDeadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    return Failure("Timed out awaiting a complete request.", "request_timeout");
                }
                catch (Exception error) when (error is JsonException or InvalidDataException)
                {
                    return Failure(error.Message, "invalid_request");
                }
            }

            return await bus.RequestAsync(request, connection, token).ConfigureAwait(false);
        }

        private static async Task<JsonObject> ReadRequestAsync(Stream source, CancellationToken token)
        {
            var bytes = new byte[MaxRequest];
            var length = 0;
            while (length < bytes.Length)
            {
                var count = await source.ReadAsync(bytes.AsMemory(length), token).ConfigureAwait(false);
                if (count == 0)
                    break;

                var lineEnd = Array.IndexOf(bytes, (byte)'\n', length, count);
                if (lineEnd >= 0)
                {
                    return JsonNode.Parse(bytes.AsSpan(0, lineEnd)) as JsonObject
                        ?? throw new InvalidDataException("Expected a JSON object.");
                }

                length += count;
            }

            throw new InvalidDataException("Expected one newline-terminated JSON request of at most 64 KiB.");
        }

        private static JsonObject Failure(string message, string code) => new()
        {
            ["ok"] = false,
            ["code"] = code,
            ["error"] = message
        };
    }
}
