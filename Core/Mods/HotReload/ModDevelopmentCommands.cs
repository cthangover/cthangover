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

    /// <summary>
    /// Registry and interpreter of development commands. The debug host owns transport and dispatching;
    /// this class owns meaning: it looks up exactly the one strategy that claims a command in a namespace,
    /// runs it on the game thread and reports whether the process changed (so the reply revision advances).
    /// <para>
    /// Registration has two sources and one rule each. Kernel built-ins are discovered by reflection on the
    /// first execution and live under the empty owner; mod strategies arrive through
    /// <see cref="Cthangover.Core.Mods.Consumers.ModDevelopmentCommandConsumer"/> keyed by the mod id and
    /// leave with the mod assembly on unload, so a hot-reloaded mod can never leave a stale command behind.
    /// A request names its owner with the optional <c>provider</c> field — omitting it addresses the kernel
    /// namespace, which is why built-ins must not rely on mod namespaces.
    /// </para>
    /// <para>
    /// Dispatch is deliberately strict: zero or several matching strategies is a refusal, never a guess, and
    /// a transition already in progress refuses every command except <c>status</c>. The registry keeps no
    /// per-connection state — the host serializes calls — so it remains safe under any transport.
    /// </para>
    /// </summary>
    internal static class ModDevelopmentCommands
    {
        private static readonly Dictionary<string, List<IModDevelopmentCommand>> Strategies = new(StringComparer.Ordinal);
        private static bool builtInsRegistered;
        private static long revision;

        /// <summary>Registers one strategy under its owner namespace (empty for kernel built-ins).</summary>
        internal static void Add(string owner, IModDevelopmentCommand strategy)
        {
            if (!Strategies.TryGetValue(owner, out var entries))
                Strategies.Add(owner, entries = new List<IModDevelopmentCommand>());
            entries.Add(strategy);
        }

        /// <summary>Removes one strategy; the owner namespace disappears with its last strategy.</summary>
        internal static void Remove(string owner, IModDevelopmentCommand strategy)
        {
            if (Strategies.TryGetValue(owner, out var entries) && entries.Remove(strategy) && entries.Count == 0)
                Strategies.Remove(owner);
        }

        /// <summary>
        /// Enumerates every command visible to the requested namespace (empty owner = all providers,
        /// a loaded mod id = that mod's strategies) sorted by owner, then command.
        /// </summary>
        internal static List<ModDevelopmentCommandEntry> Describe(string owner)
        {
            var rows = new List<ModDevelopmentCommandEntry>();
            foreach (var pair in Strategies)
            {
                if (owner.Length > 0 && pair.Key != owner)
                {
                    continue;
                }

                foreach (var strategy in pair.Value)
                {
                    foreach (var command in strategy.Commands)
                    {
                        rows.Add(new ModDevelopmentCommandEntry(pair.Key, command, strategy.Description));
                    }
                }
            }

            return rows.OrderBy(row => row.Owner, StringComparer.Ordinal)
                .ThenBy(row => row.Command, StringComparer.Ordinal).ToList();
        }

        private static JsonObject Failure(string message) => new() { ["ok"] = false, ["error"] = message };

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
                return Failure("A game transition is already in progress.");

            if (!Strategies.TryGetValue(owner, out var entries))
                return Failure("No command provider: " + owner);
            var matches = entries.Where(strategy => strategy.Commands.Contains(command, StringComparer.Ordinal)).ToArray();
            if (matches.Length != 1)
                return Failure(matches.Length == 0 ? "Unknown command: " + command : "Ambiguous command: " + command);

            var result = await matches[0].ExecuteAsync(context, command, token);
            if (context.Changed)
                revision++;
            return new JsonObject { ["ok"] = context.Succeeded, ["revision"] = revision, ["result"] = result?.DeepClone() };
        }
    }
}
