namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Memory;

    /// <summary>Exposes read-only snapshots of registered stores and the shared managed heap.</summary>
    public sealed class ModDevelopmentMemoryCommand : IModDevelopmentCommand
    {
        /// <inheritdoc />
        public IReadOnlyCollection<string> Commands { get; } = new[] { "memory.stats", "memory.inspect" };

        /// <inheritdoc />
        public string Description => "memory.stats reads the shared managed heap and paged metrics for registered stores (optional mod, offset and limit). "
            + "memory.inspect reads one store and its opted-in contents (mod, name, optional generation, offset and limit). "
            + "Use mod=__core__ for kernel stores. Limit is 1..100, preview offset is 0..9999.";

        /// <inheritdoc />
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = context.Request;
            var offset = request["offset"]?.GetValue<int>() ?? 0;
            var limit = request["limit"]?.GetValue<int>() ?? 20;
            return Task.FromResult<JsonNode>(command switch
            {
                "memory.stats" => ReadStatistics(request["mod"] == null ? null : context.ModId(), offset, limit),
                "memory.inspect" => Inspect(context, offset, limit),
                _ => throw new ArgumentException("Unknown memory command: " + command)
            });
        }

        private static JsonObject ReadStatistics(string mod, int offset, int limit)
        {
            var service = ModMemoryService.Shared;
            var (storages, total) = service.GetStatistics(mod, offset, limit);
            var entries = new JsonArray();
            foreach (var storage in storages)
                entries.Add(WriteStorage(storage));

            var heap = service.ReadManagedHeap();
            return new JsonObject
            {
                ["mod"] = mod,
                ["coverage"] = "registered_stores",
                ["managed_heap_process"] = new JsonObject
                {
                    ["estimated_bytes"] = heap.EstimatedManagedBytes,
                    ["last_gc_heap_bytes"] = JsonValue.Create(heap.LastGcHeapBytes),
                    ["last_gc_committed_bytes"] = JsonValue.Create(heap.LastGcCommittedBytes)
                },
                ["storages"] = entries,
                ["registered_count"] = total,
                ["next_offset"] = storages.Count < total - offset ? JsonValue.Create(offset + storages.Count) : null
            };
        }

        private static JsonObject Inspect(ModDevelopmentContext context, int offset, int limit)
        {
            var mod = context.ModId();
            var name = context.Require("name");
            var generation = context.Request["generation"]?.GetValue<long>();
            var result = ModMemoryService.Shared.Inspect(mod, name, offset, limit, generation);
            if (!result.Storage.HasValue)
            {
                context.Succeeded = false;
                return new JsonObject { ["mod"] = mod, ["name"] = name, ["error"] = "Storage is not registered for this owner and generation." };
            }

            var storage = result.Storage.Value;
            context.Succeeded = storage.Error == null;
            var response = WriteStorage(storage);
            if (result.Entries != null)
            {
                var preview = new JsonArray();
                foreach (var entry in result.Entries)
                    preview.Add(new JsonObject { ["key"] = entry.Key, ["value"] = entry.Value });
                response["entries"] = preview;
            }
            else
                response["entries"] = null;

            response["next_offset"] = JsonValue.Create(result.NextOffset);
            response["preview_truncated"] = result.Truncated;
            return response;
        }

        private static JsonObject WriteStorage(StorageSnapshot storage)
        {
            var result = new JsonObject
            {
                ["mod"] = storage.ModId,
                ["generation"] = storage.Generation,
                ["name"] = storage.Name,
                ["is_cache"] = storage.IsCache
            };

            if (storage.Error != null)
                result["error"] = storage.Error;
            if (!storage.Metrics.HasValue)
                return result;

            var metrics = storage.Metrics.Value;
            result["count"] = metrics.Count;
            result["capacity"] = JsonValue.Create(metrics.Capacity);
            result["estimated_payload_bytes"] = JsonValue.Create(metrics.EstimatedPayloadBytes);
            if (metrics.Cache is CacheMetrics cache)
            {
                result["cache"] = new JsonObject
                {
                    ["hits"] = JsonValue.Create(cache.Hits),
                    ["misses"] = JsonValue.Create(cache.Misses),
                    ["evictions"] = JsonValue.Create(cache.Evictions),
                    ["budget_bytes"] = JsonValue.Create(cache.BudgetBytes)
                };
            }
            return result;
        }
    }
}
