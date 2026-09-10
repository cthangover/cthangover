using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Interactive;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects interactive object definitions from mods.
    /// Interactives are environment objects that the player can interact
    /// with in scenes — they are defined in <c>.json</c> files under
    /// <c>interactives/</c> directories. Each definition specifies an
    /// interaction type, position, appearance, and behaviour.
    ///
    /// Collected in mod load order — later mods override earlier ones
    /// for the same interactive ID, enabling mods to replace or extend
    /// base game interactives.
    /// </summary>
    public class InteractiveResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "interactives";

        private Dictionary<string, InteractiveDefinition> _cache;

        /// <summary>
        /// Collects all interactive definitions from every loaded mod,
        /// keyed by interactive ID. Later mods in load order override
        /// earlier ones for the same ID. Caches the result.
        /// </summary>
        public Dictionary<string, InteractiveDefinition> Collect()
        {
            Context.Initialize();

            if (_cache != null)
                return _cache;

            var result = new Dictionary<string, InteractiveDefinition>();

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.Mods[modId].FileProvider;
                CollectInteractiveDefsRecursive(provider, modId, "interactives", result);
            }

            _cache = result;
            GameLogger.Log("MODS", $"CollectInteractives -> {result.Count} definition(s)");
            return result;
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _cache = null;
        }

        private void CollectInteractiveDefsRecursive(IModFileProvider provider, string modId, string dir, Dictionary<string, InteractiveDefinition> result)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectInteractiveDefsRecursive(provider, modId, subDir, result);
                    continue;
                }

                if (!entry.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = Context.ReadResolvedText(modId, entry, provider);
                if (text == null)
                    continue;

                try
                {
                    var fileData = JsonSerializer.Deserialize<FileData<InteractiveDefinition>>(text, JsonOptionsProvider.ReadOptions);
                    if (fileData?.Items == null || fileData.Items.Count == 0)
                        continue;

                    foreach (var item in fileData.Items)
                    {
                        if (string.IsNullOrEmpty(item.ID))
                            continue;

                        item.ModId = modId;

                        if (result.ContainsKey(item.ID))
                        {
                            GameLogger.Log("MODS", $"interactive '{item.ID}' from mod '{modId}' overrides previous definition", LogLevel.Warning);
                            ModConflictRegistry.Record("interactive", item.ID, modId, result[item.ID].ModId, false);
                        }

                        result[item.ID] = item;
                        GameLogger.Log("MODS", $"interactive '{item.ID}' from mod '{modId}' ({entry})");
                    }
                }
                catch (JsonException ex)
                {
                    GameLogger.Log("MODS", $"JSON parse failed for interactive '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                }
            }
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="InteractiveResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class Interactives
    {
        public static Dictionary<string, InteractiveDefinition> Collect()
            => ModManager.Instance.GetResolver<InteractiveResolver>()?.Collect();
    }
}
