namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Stages and applies one mod and affected dependencies through the ordinary mod lifecycle.</summary>
    public sealed class ModDevelopmentReloadCommand : IModDevelopmentCommand
    {
        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "mod.attach", "mod.reload" };

        /// <inheritdoc />
        public async Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var checkpoint = context.Request["checkpoint"]?.GetValue<bool>() == true;
            var watch = Stopwatch.StartNew();
            var reload = await ModHotReloadService.ReloadDevelopmentAsync(context.Tree, context.ModId(),
                command == "mod.attach", checkpoint, token);
            if (reload.Applied)
                context.MarkChanged();
            context.Succeeded = reload.Applied && reload.Leaked.Count == 0;
            return new JsonObject
            {
                ["applied"] = reload.Applied,
                ["elapsed_ms"] = watch.Elapsed.TotalMilliseconds,
                ["loaded"] = JsonSerializer.SerializeToNode(reload.Plan?.Load ?? Array.Empty<string>()),
                ["unloaded"] = JsonSerializer.SerializeToNode(reload.Plan?.Unload ?? Array.Empty<string>()),
                ["kept"] = JsonSerializer.SerializeToNode(reload.Plan?.Keep ?? Array.Empty<string>()),
                ["leaked"] = JsonSerializer.SerializeToNode(reload.Leaked),
                ["errors"] = JsonSerializer.SerializeToNode(reload.Errors)
            };
        }
    }
}
