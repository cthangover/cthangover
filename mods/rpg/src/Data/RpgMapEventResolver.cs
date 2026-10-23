using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Collects map events from <c>map_events/&lt;map id&gt;/**/*.json</c> of every
    /// loaded mod in load order (files of one mod sorted by path), then from the
    /// studio overlays, and merges them per map with <see cref="RpgMapEventMerge"/>.
    /// <para/>
    /// Events live apart from the map file (<c>maps/&lt;id&gt;.json</c> holds only
    /// tiles, collision, spawn and encounters) so that any mod can put its own NPC,
    /// chest or quest trigger on another mod's map without replacing the map, and
    /// so that drawing and placing do not fight over one big file. Events of a map
    /// no mod defines are kept and simply never shown.
    /// </summary>
    public class RpgMapEventResolver : ModAssetResolver
    {
        /// <summary>Group folder of event files.</summary>
        public const string Directory = "map_events";

        private Dictionary<string, List<RpgMapEvent>> cache;

        /// <inheritdoc />
        public override string Id => "rpg_map_events";

        /// <summary>Events per map id, merged; cached until the next mod reload or <see cref="ClearCache"/>.</summary>
        public Dictionary<string, List<RpgMapEvent>> Collect()
        {
            Context.Initialize();
            if (cache != null)
                return cache;

            var merge = new RpgMapEventMerge();
            foreach (var modId in Context.GetOrderedModIds())
                CollectMod(Context.Mods[modId].FileProvider, modId, merge);
            foreach (var overlay in ModOverlays.All)
                CollectMod(overlay.Value, overlay.Key, merge);

            cache = merge.Maps;
            GameLogger.Log("RPG", $"map events: {cache.Values.Sum(l => l.Count)} event(s) on {cache.Count} map(s)");
            return cache;
        }

        /// <inheritdoc />
        public override void ClearCache() => cache = null;

        private void CollectMod(IModFileProvider provider, string modId, RpgMapEventMerge merge)
        {
            foreach (var mapDir in provider.ListFiles(Directory).Where(e => e.EndsWith("/")).OrderBy(e => e, System.StringComparer.Ordinal))
            {
                var map = mapDir.TrimEnd('/');
                map = map.Substring(map.LastIndexOf('/') + 1);
                var files = new List<string>();
                ListJson(provider, mapDir.TrimEnd('/'), files);
                foreach (var file in files.OrderBy(f => f, System.StringComparer.Ordinal))
                    ReadFile(provider, modId, map, file, merge);
            }
        }

        private static void ListJson(IModFileProvider provider, string dir, List<string> files)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                    ListJson(provider, entry.TrimEnd('/'), files);
                else if (entry.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                    files.Add(entry);
            }
        }

        private void ReadFile(IModFileProvider provider, string modId, string map, string file, RpgMapEventMerge merge)
        {
            var text = Context.ReadResolvedText(modId, file, provider);
            if (string.IsNullOrWhiteSpace(text))
                return;
            try
            {
                var data = JsonSerializer.Deserialize<RpgMapEventFile>(text, JsonOptionsProvider.ReadOptions);
                foreach (var id in merge.Add(map, modId, file, data?.Events))
                    GameLogger.Log("RPG", $"map '{map}': event '{id}' replaced by mod '{modId}' ({file})");
            }
            catch (JsonException ex)
            {
                GameLogger.Log("RPG", $"map events '{modId}/{file}': broken JSON: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
