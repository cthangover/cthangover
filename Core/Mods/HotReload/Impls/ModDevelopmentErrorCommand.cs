namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Host;
    using Cthangover.Core.Utils;

    /// <summary>Serves the structured error tail to development-control clients.</summary>
    public sealed class ModDevelopmentErrorCommand : IModDevelopmentCommand
    {
        /// <summary>Publishes the warning and compiler diagnostic feed under <c>errors.pull</c>.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "errors.pull" };

        /// <summary>Documents advancing the caller's cursor without losing a partially returned page.</summary>
        public string Description { get; } = "errors.pull: returns warnings and compiler errors (since, max 1..512, level warning|error, optional mod). Pass the response's next value for the next page.";

        /// <summary>Applies severity and optional mod filters to a single consistent snapshot.</summary>
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var request = context.Request;
            var since = request["since"]?.GetValue<long>();
            var max = Math.Clamp(request["max"]?.GetValue<int>() ?? 200, 1, 512);
            var minimum = string.Equals(request["level"]?.GetValue<string>(), "error", System.StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Error
                : LogLevel.Warning;
            return Task.FromResult<JsonNode>(ModErrorFeed.Read(since, max, minimum,
                request["mod"] == null ? null : context.ModId()));
        }
    }
}
