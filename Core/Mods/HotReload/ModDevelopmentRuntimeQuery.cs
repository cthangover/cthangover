namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Games;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Godot;

    /// <summary>Reads the current process identity and optionally one detached save slot.</summary>
    public sealed class ModDevelopmentRuntimeQuery : IModDevelopmentCommand
    {
        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "status", "runtime.inspect" };

        /// <inheritdoc />
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
            => Task.FromResult<JsonNode>(command == "status" ? Status(context.Tree)
                : InspectRuntime(context.Tree, context.Request["mod"]?.GetValue<string>()));

        private static JsonObject Status(SceneTree tree) => new()
        {
            ["protocol"] = 1,
            ["pid"] = System.Environment.ProcessId,
            ["game"] = ActiveGame.Id,
            ["scene"] = SceneManager.GetCurrentSceneName(),
            ["paused"] = tree.Paused,
            ["scene_instance"] = tree.CurrentScene?.GetInstanceId(),
            ["mods"] = JsonSerializer.SerializeToNode(ModRegistry.Instance.Mods.Keys.Order(StringComparer.Ordinal))
        };

        private static JsonObject InspectRuntime(SceneTree tree, string slot)
        {
            var result = Status(tree);
            var runtime = GameData.Instance.Runtime;
            result["game_minute"] = runtime?.Time.Tick;
            if (slot != null)
            {
                var snapshot = SaveService.BuildPackage(SaveService.HotReloadSlot);
                if (!snapshot.ModData.TryGetValue(slot, out var data))
                    throw new ArgumentException("No runtime save slot named '" + slot + "'.");
                result["slot"] = slot;
                result["state"] = JsonSerializer.SerializeToNode(data);
            }
            return result;
        }
    }
}
