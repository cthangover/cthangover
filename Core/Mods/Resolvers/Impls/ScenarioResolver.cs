using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects and resolves scenario script files (<c>.scenario</c>) from
    /// mods. Scenarios are Lua-like scripts that drive gameplay events on
    /// specific scenes. Each <c>.scenario</c> file contains YAML-like
    /// metadata headers followed by the script body.
    ///
    /// <see cref="CollectDefinitions"/> scans every mod's
    /// <c>scenarios/</c> folder recursively and groups scenario
    /// definitions by their target scene name. Resolution returns the
    /// script text for a given scenario ID, with <c>${include}</c>
    /// directives already expanded.
    /// </summary>
    public class ScenarioResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "scenarios";

        private Dictionary<string, List<ScenarioDefinition>> _defCache;

        /// <summary>
        /// Collects all scenario definitions from every loaded mod,
        /// grouped by target scene name. Each definition contains
        /// the scenario name, scene, mod ID, file path, priority,
        /// dependencies, conditions, and other metadata parsed from the
        /// <c>.scenario</c> file header.
        /// </summary>
        public Dictionary<string, List<ScenarioDefinition>> CollectDefinitions()
        {
            Context.Initialize();

            if (_defCache != null)
                return _defCache;

            var result = new Dictionary<string, List<ScenarioDefinition>>();

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.Mods[modId].FileProvider;
                CollectScenarioDefsRecursive(provider, modId, "scenarios", result);
            }

            _defCache = result;

            GameLogger.Log("MODS", $"CollectScenarioDefinitions → {result.Count} scene(s) with scenarios");
            return result;
        }

        /// <summary>
        /// Resolves a scenario by its ID (name), returning the full
        /// script text with <c>${include}</c> directives expanded.
        /// Searches across all mods' scenario definitions.
        /// </summary>
        public string Resolve(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return null;

            var defs = CollectDefinitions();
            foreach (var list in defs.Values)
            {
                foreach (var def in list)
                {
                    if (def.Name == scenarioId)
                    {
                        var text = Context.ReadResolvedText(def.ModId, def.FilePath);
                        if (text == null)
                        {
                            GameLogger.Log("MODS", $"ResolveScenario: '{scenarioId}' from mod '{def.ModId}' could not be read", LogLevel.Error);
                            return null;
                        }

                        return text;
                    }
                }
            }

            GameLogger.Log("MODS", $"ResolveScenario: '{scenarioId}' not found in any mod", LogLevel.Error);
            return null;
        }

        /// <summary>
        /// Resolves a scenario using an explicit provider override.
        /// Useful when the scenario must be read from an unpacked or
        /// temporary file source rather than the mod's built-in provider.
        /// </summary>
        public string Resolve(string scenarioId, IModFileProvider provider)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return null;

            var defs = CollectScenarioDefsFromProvider(provider, "scenarios");
            foreach (var def in defs)
            {
                if (def.Name == scenarioId)
                {
                    var text = Context.ReadResolvedText("__override__", def.FilePath, provider);
                    if (text == null)
                    {
                        GameLogger.Log("MODS", $"ResolveScenario: '{scenarioId}' could not be read from override provider", LogLevel.Error);
                        return null;
                    }
                    return text;
                }
            }

            GameLogger.Log("MODS", $"ResolveScenario: '{scenarioId}' not found in override provider", LogLevel.Error);
            return null;
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _defCache = null;
        }

        private static List<ScenarioDefinition> CollectScenarioDefsFromProvider(IModFileProvider provider, string dir)
        {
            var result = new List<ScenarioDefinition>();
            CollectScenarioDefsFromProviderRecursive(provider, "__override__", dir, result);
            return result;
        }

        private static void CollectScenarioDefsFromProviderRecursive(IModFileProvider provider, string modId, string dir, List<ScenarioDefinition> result)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectScenarioDefsFromProviderRecursive(provider, modId, subDir, result);
                    continue;
                }

                if (!entry.EndsWith(".scenario", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = provider.ReadFileText(entry);
                if (text == null)
                    continue;

                var (metadata, _) = Scenarios.ScenarioParser.ParseMetadata(text);
                if (!metadata.TryGetValue("scene", out var sceneName) || string.IsNullOrEmpty(sceneName))
                    continue;

                var name = Path.GetFileNameWithoutExtension(entry.Replace('\\', '/').Split('/')[^1]);

                result.Add(new ScenarioDefinition
                {
                    Name = name,
                    Scene = sceneName,
                    ModId = modId,
                    FilePath = entry,
                });
            }
        }

        private void CollectScenarioDefsRecursive(IModFileProvider provider, string modId, string dir, Dictionary<string, List<ScenarioDefinition>> result)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectScenarioDefsRecursive(provider, modId, subDir, result);
                    continue;
                }

                if (!entry.EndsWith(".scenario", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = provider.ReadFileText(entry);
                if (text == null)
                    continue;

                var (metadata, _) = Scenarios.ScenarioParser.ParseMetadata(text);
                if (!metadata.TryGetValue("scene", out var sceneName) || string.IsNullOrEmpty(sceneName))
                    continue;

                var name = Path.GetFileNameWithoutExtension(entry.Replace('\\', '/').Split('/')[^1]);

                var def = new ScenarioDefinition
                {
                    Name = name,
                    Scene = sceneName,
                    ModId = modId,
                    FilePath = entry,
                    Priority = metadata.TryGetValue("priority", out var p) && int.TryParse(p, out var prio) ? prio : 0,
                    After = metadata.TryGetValue("after", out var after) ? after : null,
                    Condition = metadata.TryGetValue("condition", out var cond) ? cond : null,
                    LightUseTime = metadata.TryGetValue("light_use_time", out var l) && bool.TryParse(l, out var b) ? b : null,
                    SaveAllowed = metadata.TryGetValue("save_allowed", out var sa) && bool.TryParse(sa, out var sab),
                    IsOneRun = metadata.TryGetValue("is_one_run", out var ior) && bool.TryParse(ior, out var iorb),
                };

                if (!result.TryGetValue(sceneName, out var list))
                {
                    list = new List<ScenarioDefinition>();
                    result[sceneName] = list;
                }
                list.Add(def);

                GameLogger.Log("MODS", $"scenario '{name}' -> scene '{sceneName}' from mod '{modId}' ({entry})");
            }
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="ScenarioResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class ScenarioDefs
    {
        public static Dictionary<string, List<ScenarioDefinition>> CollectDefinitions()
            => ModManager.Instance.GetResolver<ScenarioResolver>()?.CollectDefinitions();

        public static string Resolve(string id)
            => ModManager.Instance.GetResolver<ScenarioResolver>()?.Resolve(id);

        public static string Resolve(string id, IModFileProvider provider)
            => ModManager.Instance.GetResolver<ScenarioResolver>()?.Resolve(id, provider);
    }
}
