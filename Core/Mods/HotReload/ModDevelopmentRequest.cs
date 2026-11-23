namespace Cthangover.Core.Mods.HotReload
{
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    internal sealed class ModDevelopmentRequest
    {
        internal ModDevelopmentRequest(JsonObject payload) => Payload = payload;
        internal JsonObject Payload { get; }
        internal TaskCompletionSource<JsonObject> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
