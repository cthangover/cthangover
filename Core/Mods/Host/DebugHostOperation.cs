namespace Cthangover.Core.Mods.Host
{
    using System;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Holds one reconnectable result without retaining the submitting client's cancellation token.</summary>
    internal sealed class DebugHostOperation
    {
        private readonly JsonObject request;
        private int started;

        internal DebugHostOperation(JsonObject request)
        {
            this.request = ComparableRequest(request);
        }

        internal TaskCompletionSource<JsonObject> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool Matches(JsonObject request) => JsonNode.DeepEquals(this.request, ComparableRequest(request));

        internal void MarkStarted() => Volatile.Write(ref started, 1);

        internal async Task<JsonObject> WaitAsync(CancellationToken cancellationToken)
        {
            var answer = await Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return (JsonObject)answer.DeepClone();
        }

        internal JsonObject Status(Guid id)
        {
            var result = new JsonObject { ["operation_id"] = id.ToString("D") };
            var task = Completion.Task;
            if (task.IsCompletedSuccessfully)
            {
                result["state"] = "completed";
                result["response"] = task.Result.DeepClone();
            }
            else if (task.IsCanceled)
            {
                result["state"] = "cancelled";
            }
            else
            {
                result["state"] = Volatile.Read(ref started) != 0 ? "running" : "queued";
            }

            return new JsonObject { ["ok"] = true, ["result"] = result };
        }

        private static JsonObject ComparableRequest(JsonObject request)
        {
            var comparable = (JsonObject)request.DeepClone();
            comparable.Remove("client");
            return comparable;
        }
    }
}
