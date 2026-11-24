namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Games;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Reads the current process identity and optionally one detached save slot.</summary>
    public sealed class ModDevelopmentRuntimeQuery : IModDevelopmentCommand
    {
        /// <summary>Exposes process status and detached runtime state under separate command names.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "status", "runtime.inspect" };

        /// <summary>Distinguishes process identity from a game runtime or save slot snapshot.</summary>
        public string Description { get; } = "status: process identity, boot phase and active legacy-pipe connection count. runtime.inspect: reads live game state and an optional detached save slot.";

        /// <summary>Reads runtime objects only while the kernel dispatcher is on the game thread.</summary>
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
            => Task.FromResult<JsonNode>(command == "status" ? Status(context.Tree)
                : InspectRuntime(context.Tree, context.Request["mod"]?.GetValue<string>()));

        internal static JsonObject Status(SceneTree tree) => new()
        {
            ["protocol"] = 1,
            ["instance"] = Cthangover.Core.Mods.Host.DebugHostInstance.Id.ToString(),
            ["pid"] = System.Environment.ProcessId,
            ["phase"] = Boot.Phase.ToString(),
            ["project_root"] = GamePaths.Globalize("res://"),
            ["connections"] = Cthangover.Core.Mods.Host.DebugHost.ActiveConnections,
            ["game"] = ActiveGame.Id,
            ["scene"] = SceneManager.GetCurrentSceneName(),
            ["paused"] = tree.Paused,
            ["scene_instance"] = tree.CurrentScene?.GetInstanceId(),
            ["mods"] = JsonSerializer.SerializeToNode(ModRegistry.Instance.Mods.Keys.Order(StringComparer.Ordinal))
        };

        internal static JsonObject InspectRuntime(SceneTree tree, string slot)
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
