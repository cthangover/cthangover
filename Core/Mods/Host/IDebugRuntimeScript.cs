namespace Cthangover.Core.Mods.Host
{
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>One entry point in a trusted, one-shot C# assembly created by the debug host.</summary>
    public interface IDebugRuntimeScript
    {
        /// <summary>
        /// Starts on the game thread. Return plain JSON and cooperate with cancellation. Release
        /// temporary references and subscriptions before returning so the assembly can be collected.
        /// </summary>
        Task<JsonNode> RunAsync(DebugScriptContext context, CancellationToken cancellationToken);
    }
}
