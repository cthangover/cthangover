using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Games;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects scene definitions from mods. A scene definition describes
    /// a playable scene — its name, default ambient, default scenario,
    /// and backgrounds. Mods can define scenes in <c>scenes/</c> via
    /// <c>.json</c> files. When multiple mods define the same scene name,
    /// the first mod's definition is the base, and subsequent mods merge
    /// their values on top — enabling mods to extend a base scene with
    /// additional backgrounds or change the default scenario. Overlays
    /// (<see cref="ModOverlays"/>) are read after the mods, so an overlay can
    /// add new scenes or extend existing ones the same way.
    /// </summary>
    public class SceneDefinitionResolver : ModAssetResolver
    {
        /// <summary>Resolver key used by the mod manager's registration and lookup pipeline.</summary>
        public override string Id => "scene_definitions";

        private Dictionary<string, List<SceneDefinition>> _cache;

        /// <summary>
        /// Collects all scene definitions from every loaded mod. The first
        /// mod that defines a scene name becomes the base; subsequent mods
        /// merge extra backgrounds and override the default ambient and
        /// scenario via <c>MergeSceneDef</c>. Caches the result. Frontend-only
        /// launches may have no playable scenes; other modes retain the empty-catalogue error.
        /// </summary>
        public Dictionary<string, List<SceneDefinition>> Collect()
        {
            Context.Initialize();

            if (_cache != null)
                return _cache;

            var result = new Dictionary<string, List<SceneDefinition>>();
            var foundAny = false;

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.Mods[modId].FileProvider;
                CollectSceneDefsRecursive(provider, modId, "scenes", result, ref foundAny);
            }

            foreach (var overlay in ModOverlays.All)
                CollectSceneDefsRecursive(overlay.Value, overlay.Key, "scenes", result, ref foundAny);

            if (!foundAny && GameLaunchArgs.Current.Mode != GameLaunchMode.Frontend)
            {
                GameLogger.Log("MODS", "CollectSceneDefinitions — no scene definitions found", LogLevel.Error);
            }

            _cache = result;
            GameLogger.Log("MODS", $"CollectSceneDefinitions → {result.Count} scene(s)");
            return result;
        }

        /// <summary>Drops the collected scene table before a content or mod-set replacement.</summary>
        public override void ClearCache()
        {
            _cache = null;
        }

        private void CollectSceneDefsRecursive(IModFileProvider provider, string modId, string dir,
            Dictionary<string, List<SceneDefinition>> result, ref bool foundAny)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectSceneDefsRecursive(provider, modId, subDir, result, ref foundAny);
                    continue;
                }

                if (!entry.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = Context.ReadResolvedText(modId, entry, provider);
                if (text == null)
                    continue;

                try
                {
                    var def = JsonSerializer.Deserialize<SceneDefinition>(text, JsonOptionsProvider.ReadOptions);
                    if (def == null || string.IsNullOrEmpty(def.Name))
                        continue;

                    def.ModId = modId;
                    foundAny = true;

                    if (!result.TryGetValue(def.Name, out var list))
                    {
                        list = new List<SceneDefinition>();
                        result[def.Name] = list;
                        list.Add(def);
                    }
                    else
                    {
                        var baseDef = list[0];
                        MergeSceneDef(baseDef, def);
                        GameLogger.Log("MODS",
                            $"scene '{def.Name}' from mod '{modId}' merged with base from '{baseDef.ModId}'");
                    }

                    GameLogger.Log("MODS", $"scene '{def.Name}' from mod '{modId}' ({entry})");
                }
                catch (JsonException ex)
                {
                    GameLogger.Log("MODS", $"JSON parse failed for scene '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                }
            }
        }

        private static void MergeSceneDef(SceneDefinition baseDef, SceneDefinition incoming)
        {
            if (!string.IsNullOrWhiteSpace(incoming.Shell)) baseDef.Shell = incoming.Shell;
            if (incoming.SaveAllowed.HasValue) baseDef.SaveAllowed = incoming.SaveAllowed;
            if (!string.IsNullOrEmpty(incoming.DefaultAmbient))
                baseDef.DefaultAmbient = incoming.DefaultAmbient;
            if (!string.IsNullOrEmpty(incoming.DefaultScenario))
                baseDef.DefaultScenario = incoming.DefaultScenario;

            if (incoming.DefaultBackground?.Count > 0)
            {
                baseDef.DefaultBackground ??= new List<string>();
                baseDef.DefaultBackground.AddRange(incoming.DefaultBackground);
            }
        }
    }

}
