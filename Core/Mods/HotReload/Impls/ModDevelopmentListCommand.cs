namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Lists the strategies currently registered in the kernel or one mod namespace.</summary>
    public sealed class ModDevelopmentListCommand : IModDevelopmentCommand
    {
        /// <summary>Offers the legacy complete command list and a paged command search.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "commands", "describe" };

        /// <summary>Explains the filters an agent can use when searching registered commands.</summary>
        public string Description { get; } = "describe: searches commands by query and optional mod, with offset and limit. commands: lists registered commands and their owners.";

        /// <summary>Reads the registry on the game thread and returns a detached list or search page.</summary>
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var owner = context.Request["mod"] == null ? "" : context.ModId();
            var entries = ModDevelopmentCommands.Describe(owner);
            if (command == "commands")
                return Task.FromResult(JsonSerializer.SerializeToNode(entries));

            var query = context.Request["query"]?.GetValue<string>() ?? "";
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var offset = Math.Max(0, context.Request["offset"]?.GetValue<int>() ?? 0);
            var limit = Math.Clamp(context.Request["limit"]?.GetValue<int>() ?? 20, 1, 100);
            var matches = new List<ModDevelopmentCommandEntry>();
            foreach (var entry in entries)
            {
                if (Matches(entry, words))
                    matches.Add(entry);
            }
            var items = matches.Skip(offset).Take(limit).ToArray();
            return Task.FromResult<JsonNode>(new JsonObject
            {
                ["entries"] = JsonSerializer.SerializeToNode(items),
                ["total"] = matches.Count,
                ["next_offset"] = items.Length != 0 && items.Length < matches.Count - offset
                    ? JsonValue.Create(offset + items.Length) : null
            });
        }

        private static bool Matches(ModDevelopmentCommandEntry entry, string[] words)
        {
            foreach (var word in words)
            {
                if (!entry.Command.Contains(word, StringComparison.OrdinalIgnoreCase)
                    && !entry.Owner.Contains(word, StringComparison.OrdinalIgnoreCase)
                    && !(entry.Description?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false))
                    return false;
            }
            return true;
        }
    }
}
