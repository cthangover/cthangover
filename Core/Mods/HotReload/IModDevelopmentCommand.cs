namespace Cthangover.Core.Mods.HotReload
{
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A development command strategy, discovered from public parameterless classes. Mod strategies
    /// are namespaced by mod ID and leave the command registry with their assembly.
    /// </summary>
    public interface IModDevelopmentCommand
    {
        /// <summary>Command names claimed within the strategy's namespace.</summary>
        IReadOnlyCollection<string> Commands { get; }

        /// <summary>
        /// Human-readable description surfaced by the <c>describe</c> query. Cover the claimed commands
        /// and their arguments so an agent can use them without reading the provider source.
        /// </summary>
        string Description => string.Join(", ", Commands);

        /// <summary>Runs on the game thread. Return a detached JSON result for the caller.</summary>
        Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token);
    }
}
