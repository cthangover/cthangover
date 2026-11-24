namespace Cthangover.Core.Mods.Host
{
    using System;
    using System.IO;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Games;
    using Cthangover.Core.Games.LaunchParameters;
    using Cthangover.Core.Mods.HotReload;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Process-wide dispatcher for debug commands. It serializes access to the live game on the game
    /// thread, regardless of whether an in-process mod or the optional local pipe submitted the request.
    /// Loading a mod can add its own transport without opening the legacy pipe.
    /// </summary>
    public sealed partial class DebugHost : Node
    {
        private static DebugHost current;

        private readonly CancellationTokenSource stop = new();
        private readonly DebugHostBus bus = new();
        private IDebugHostTransport legacyTransport;
        private Task active = Task.CompletedTask;
        private Task shutdown;

        /// <summary>Installed host of this process; null before boot installs it or during shutdown.</summary>
        internal static DebugHost Current => Volatile.Read(ref current);

        /// <summary>Live legacy pipe connection count of the installed host, reported by status.</summary>
        internal static int ActiveConnections => Current?.bus.ActiveConnections ?? 0;

        /// <summary>
        /// Queues a detached command for the game thread and awaits the registry's JSON result. The
        /// caller may use this API from a transport thread. Cancelling the wait also cancels pending
        /// execution or signals cancellation to an already running strategy.
        /// </summary>
        public static Task<JsonObject> ExecuteAsync(JsonObject request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<JsonObject>(cancellationToken);

            var host = Current;
            return host != null
                ? host.bus.RequestAsync(request, 0, cancellationToken)
                : Task.FromResult(Failure("The debug host is not ready.", "host_unavailable"));
        }

        /// <summary>Installs the dispatcher after boot, with game-thread processing even while paused.</summary>
        internal static void Install(SceneTree tree)
        {
            if (GameEnvironment.IsRuntime && tree != null)
                ModRoot.AddDeferred(new DebugHost { Name = "DebugHost", ProcessMode = ProcessModeEnum.Always });
        }

        /// <summary>Publishes the in-process dispatcher and starts the legacy pipe when enabled.</summary>
        public override void _Ready()
        {
            Volatile.Write(ref current, this);
            ApplicationExit.Preparing += Shutdown;
            var launch = GameLaunchArgs.Current;
            if (!launch.Contains<NoDevelopmentControlParameter>()
                && (launch.Contains<DevelopmentControlParameter>() || ModConfig.Instance.DevelopmentControl))
            {
                StartLegacyTransport();
            }
        }

        /// <summary>Starts at most one command per frame and lets it complete before admitting the next.</summary>
        public override void _Process(double delta)
        {
            if (stop.IsCancellationRequested || !active.IsCompleted || !bus.TryReceive(out var packet))
                return;

            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            active = finished.Task;
            _ = Dispatch(packet, finished);
        }

        private void StartLegacyTransport()
        {
            var transport = new Transport.Impls.DebugLocalPipeTransport();
            try
            {
                transport.Start(bus);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                DebugHostInstance.Log("could not open the development pipe: " + error.Message, LogLevel.Warning);
                return;
            }

            legacyTransport = transport;
            var discoveryPath = ModConfig.Instance.DevelopmentDiscovery;
            if (!string.IsNullOrWhiteSpace(discoveryPath))
            {
                try
                {
                    new DebugHostDiscoveryFile(discoveryPath).Publish(new[] { transport.Endpoint }, System.Environment.ProcessId);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                {
                    DebugHostInstance.Log("could not publish the development pipe: " + error.Message, LogLevel.Warning);
                }
            }

            DebugHostInstance.Log("debug host ready: instance=" + DebugHostInstance.Id + "; endpoints: "
                + transport.Name + "=" + transport.Endpoint.Address);
        }

        private async Task Dispatch(DebugHostPacket packet, TaskCompletionSource finished)
        {
            try
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, packet.Cancellation);
                cancellation.Token.ThrowIfCancellationRequested();
                if (packet.Connection != 0)
                {
                    var client = packet.Payload["client"]?.GetValue<string>();
                    DebugHostInstance.Log("handshake connection=" + packet.Connection
                        + (string.IsNullOrWhiteSpace(client) ? "" : " client=" + client)
                        + " command=" + (packet.Payload["command"]?.GetValue<string>() ?? "?")
                        + " (active " + bus.ActiveConnections + ")");
                }

                var answer = await ModDevelopmentCommands.ExecuteAsync(GetTree(), packet.Payload, cancellation.Token);
                Complete(packet, answer);
            }
            catch (OperationCanceledException error) when (packet.Cancellation.IsCancellationRequested)
            {
                if (packet.Completion != null)
                    packet.Completion.TrySetCanceled(packet.Cancellation);
                else
                    Complete(packet, Failure(error.Message, "cancelled"));
            }
            catch (Exception error)
            {
                if (error is not (ArgumentException or InvalidOperationException or OperationCanceledException))
                    DebugHostInstance.Log(error.ToString(), LogLevel.Warning);
                Complete(packet, Failure(error.Message));
            }
            finally
            {
                finished.TrySetResult();
            }
        }

        private void Complete(DebugHostPacket packet, JsonObject answer)
        {
            if (packet.Completion != null)
                packet.Completion.TrySetResult(answer);
            else
                bus.Deliver(new DebugHostPacket(DebugHostPacketKind.Response, packet.Connection, "", answer));
        }

        private static JsonObject Failure(string message, string code = null) => new()
        {
            ["ok"] = false,
            ["code"] = code,
            ["error"] = message
        };

        private Task Shutdown(SceneTree tree)
        {
            if (shutdown != null)
                return shutdown;

            Interlocked.CompareExchange(ref current, null, this);
            bus.CloseRequests();
            shutdown = Task.WhenAll(stop.CancelAsync(), legacyTransport?.StopAsync() ?? Task.CompletedTask, active);
            return shutdown;
        }

        /// <summary>Unregisters from engine shutdown and retires the dispatcher if the tree removed it.</summary>
        public override void _ExitTree()
        {
            ApplicationExit.Preparing -= Shutdown;
            _ = DisposeAfterShutdownAsync(Shutdown(null), stop);
        }

        private static async Task DisposeAfterShutdownAsync(Task shutdown, CancellationTokenSource stop)
        {
            try
            {
                await shutdown.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                DebugHostInstance.Log("debug host shutdown failed: " + error.Message, LogLevel.Warning);
            }
            finally
            {
                stop.Dispose();
            }
        }
    }
}
