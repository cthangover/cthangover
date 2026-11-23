namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Games;
    using Cthangover.Core.Utils;
    using Godot;

    internal static class ModDevelopmentCommands
    {
        private static readonly Dictionary<string, List<IModDevelopmentCommand>> Strategies = new(StringComparer.Ordinal);
        private static bool builtInsRegistered;
        private static long revision;

        internal static void Add(string owner, IModDevelopmentCommand strategy)
        {
            if (!Strategies.TryGetValue(owner, out var entries))
                Strategies.Add(owner, entries = new List<IModDevelopmentCommand>());
            entries.Add(strategy);
        }

        internal static void Remove(string owner, IModDevelopmentCommand strategy)
        {
            if (Strategies.TryGetValue(owner, out var entries) && entries.Remove(strategy) && entries.Count == 0)
                Strategies.Remove(owner);
        }

        internal static string[] List(string owner)
        {
            return Strategies.TryGetValue(owner, out var entries)
                ? entries.SelectMany(strategy => strategy.Commands).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
        }

        internal static async Task<JsonObject> ExecuteAsync(SceneTree tree, JsonObject request, CancellationToken token)
        {
            if (!builtInsRegistered)
            {
                foreach (var strategy in Reflections.FindAndCreate<IModDevelopmentCommand>(typeof(ModDevelopmentCommands).Assembly))
                    Add("", strategy);
                builtInsRegistered = true;
            }

            var context = new ModDevelopmentContext(tree, request);
            var command = context.Require("command");
            var owner = request["provider"]?.GetValue<string>() ?? "";
            if ((owner != "" || command != "status")
                && (ModHotReloadService.IsReloadRunning || GameSession.IsSwitching || ModManager.Instance.IsReloading))
                return ModDevelopmentControl.Failure("A game transition is already in progress.");

            if (!Strategies.TryGetValue(owner, out var entries))
                return ModDevelopmentControl.Failure("No command provider: " + owner);
            var matches = entries.Where(strategy => strategy.Commands.Contains(command, StringComparer.Ordinal)).ToArray();
            if (matches.Length != 1)
                return ModDevelopmentControl.Failure(matches.Length == 0 ? "Unknown command: " + command : "Ambiguous command: " + command);

            var result = await matches[0].ExecuteAsync(context, command, token);
            if (context.Changed)
                revision++;
            return new JsonObject { ["ok"] = context.Succeeded, ["revision"] = revision, ["result"] = result?.DeepClone() };
        }
    }
}
