namespace Cthangover.Core.Mods.HotReload.Impls
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
        /// <summary>Provides the attach and reload operations through the same preparation path.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "mod.attach", "mod.reload" };

        /// <summary>Explains affected dependencies and the optional live-scene checkpoint.</summary>
        public string Description { get; } = "mod.attach / mod.reload: stages and applies one development mod and its affected dependencies; attach adds it to the active game's mod set, checkpoint=true saves and restores the live scene.";

        /// <summary>Applies and verifies the change, then reports the files the active mod context runs.</summary>
        public async Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var checkpoint = context.Request["checkpoint"]?.GetValue<bool>() == true;
            var mod = context.ModId();
            var watch = Stopwatch.StartNew();
            var reload = await ModHotReloadService.ReloadDevelopmentAsync(context.Tree, mod,
                command == "mod.attach", checkpoint, token);
            if (reload.Applied)
                context.MarkChanged();
            context.Succeeded = reload.Applied && reload.Leaked.Count == 0;
            return new JsonObject
            {
                ["applied"] = reload.Applied,
                ["mod"] = mod,
                ["active_artifacts"] = JsonSerializer.SerializeToNode(ModAssemblyLoader.ArtifactsOf(mod)),
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
