namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.IO.Pipes;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Same-user, process-addressed JSON command pipe. The transport never touches Godot objects.
    /// One command at a time enters the game thread outside scene or gameplay signal emission.
    /// </summary>
    internal sealed partial class ModDevelopmentControl : Node
    {
        private const int MaxRequest = 64 * 1024;
        private readonly CancellationTokenSource stop = new();
        private readonly ConcurrentQueue<ModDevelopmentRequest> pending = new();
        private Task listener = Task.CompletedTask;
        private Task active = Task.CompletedTask;

        internal static void InstallIfEnabled(SceneTree tree)
        {
            if (tree != null && ModConfig.Instance.DevelopmentControl
                && Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-development-control") < 0)
                ModRoot.AddDeferred(new ModDevelopmentControl { Name = "ModDevelopmentControl", ProcessMode = ProcessModeEnum.Always });
        }

        public override void _Ready()
        {
            ApplicationExit.Preparing += Shutdown;
            var pipe = "cthangover-dev-" + System.Environment.ProcessId;
            listener = Task.Run(() => Listen(pipe, stop.Token));
            GameLogger.Log("MOD_DEV", $"Development control pipe: {pipe}");
        }

        public override void _Process(double delta)
        {
            if (!stop.IsCancellationRequested && active.IsCompleted && pending.TryDequeue(out var request))
                active = Dispatch(request);
        }

        private async Task Dispatch(ModDevelopmentRequest request)
        {
            try
            {
                var answer = await ModDevelopmentCommands.ExecuteAsync(GetTree(), request.Payload, stop.Token);
                request.Answer.TrySetResult(answer);
            }
            catch (Exception error)
            {
                if (error is not (ArgumentException or InvalidOperationException or OperationCanceledException))
                    GameLogger.Log("MOD_DEV", error.ToString(), LogLevel.Warning);
                request.Answer.TrySetResult(Failure(error.Message));
            }
        }

        private async Task Listen(string pipe, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using var stream = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await stream.WaitForConnectionAsync(token).ConfigureAwait(false);
                    try
                    {
                        using var readDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                        readDeadline.CancelAfter(TimeSpan.FromSeconds(5));
                        var payload = await ReadRequest(stream, readDeadline.Token).ConfigureAwait(false);
                        var request = new ModDevelopmentRequest(payload);
                        pending.Enqueue(request);
                        var answer = await request.Answer.Task.WaitAsync(token).ConfigureAwait(false);
                        await Respond(stream, answer, token).ConfigureAwait(false);
                    }
                    catch (Exception error) when (!token.IsCancellationRequested)
                    {
                        try { await Respond(stream, Failure(error.Message), token).ConfigureAwait(false); }
                        catch (Exception responseError) when (responseError is IOException or OperationCanceledException) { }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error)
            {
                GameLogger.Log("MOD_DEV", $"Development control stopped: {error.Message}", LogLevel.Warning);
            }
        }

        private static async Task<JsonObject> ReadRequest(Stream stream, CancellationToken token)
        {
            var bytes = new byte[MaxRequest];
            var length = 0;
            while (length < bytes.Length)
            {
                var count = await stream.ReadAsync(bytes.AsMemory(length), token).ConfigureAwait(false);
                if (count == 0)
                    break;
                var lineEnd = Array.IndexOf(bytes, (byte)'\n', length, count);
                if (lineEnd >= 0)
                    return JsonNode.Parse(bytes.AsSpan(0, lineEnd)) as JsonObject
                        ?? throw new InvalidDataException("Expected a JSON object.");
                length += count;
            }
            throw new InvalidDataException("Expected one newline-terminated JSON request of at most 64 KiB.");
        }

        private static async Task Respond(Stream stream, JsonObject answer, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            var bytes = Encoding.UTF8.GetBytes(answer.ToJsonString() + "\n");
            await stream.WriteAsync(bytes, deadline.Token).ConfigureAwait(false);
            await stream.FlushAsync(deadline.Token).ConfigureAwait(false);
        }

        internal static JsonObject Failure(string message) => new() { ["ok"] = false, ["error"] = message };

        private async Task Shutdown(SceneTree tree)
        {
            stop.Cancel();
            await Task.WhenAll(listener, active);
        }

        public override void _ExitTree()
        {
            ApplicationExit.Preparing -= Shutdown;
            stop.Cancel();
            stop.Dispose();
        }
    }
}
