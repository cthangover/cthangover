namespace Cthangover.Core.Mods.HotReload
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Lists the strategies currently registered in the kernel or one mod namespace.</summary>
    public sealed class ModDevelopmentListCommand : IModDevelopmentCommand
    {
        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "commands" };

        /// <inheritdoc />
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var owner = context.Request["mod"] == null ? "" : context.ModId(loaded: true);
            return Task.FromResult(JsonSerializer.SerializeToNode(ModDevelopmentCommands.List(owner)));
        }
    }
}
