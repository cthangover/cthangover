namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Host;
    using Cthangover.Core.Utils;

    /// <summary>Returns the process context, compiler/cache facts and a bounded error page in one call.</summary>
    public sealed class ModDevelopmentDiagnoseCommand : IModDevelopmentCommand
    {
        /// <summary>Names the aggregate command used by the MCP diagnose tool.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "diagnose" };

        /// <summary>Lists the supported mod and error cursor arguments.</summary>
        public string Description => "diagnose: startup phase, active game and related errors; optional mod, since (cursor), and limit.";

        /// <summary>Returns phase, errors and, when requested, the selected mod's compilation state.</summary>
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var mod = context.Request["mod"] == null ? null : context.ModId();
            var since = context.Request["since"]?.GetValue<long>();
            var limit = Math.Clamp(context.Request["limit"]?.GetValue<int>() ?? 8, 1, 100);
            var result = ModErrorFeed.Read(since, limit, LogLevel.Warning, mod);
            var status = ModDevelopmentRuntimeQuery.Status(context.Tree);
            status.Remove("mods");
            result["status"] = status;
            if (mod != null)
            {
                result["mod"] = ModDevelopmentInspectCommand.InspectMod(mod);
                result["cache"] = ModDevelopmentInspectCommand.InspectCompilation(mod);
            }

            return Task.FromResult<JsonNode>(result);
        }
    }
}
