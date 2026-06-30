using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Collects scene subscription definitions from mod manifests, compiles wrapper
    /// template scripts with injected user code via <see cref="ModCompiler"/>, and
    /// executes them on scene enter and exit events. Subscriptions are C# code
    /// fragments that run against the scene root node, enabling mods to dynamically
    /// populate or modify scenes. Compilation results are cached by template+code key
    /// for reuse across scene visits.
    /// </summary>
    public static class SceneSubscriptionRegistry
    {
        private static readonly Dictionary<string, List<SubscriptionInfo>> enterSubscriptions = new();
        private static readonly Dictionary<string, List<SubscriptionInfo>> exitSubscriptions = new();
        private static readonly Dictionary<string, CompileResult> compiledCache = new();
        private static bool initialized;

        /// <summary>
        /// Collects all subscription entries from mod manifests by iterating over
        /// loaded mods, reading wrapper template files and optional user code files,
        /// and populating the enter/exit subscription tables. Safe to call multiple
        /// times; subsequent calls are no-ops.
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            CollectFromMods();

            var enterTotal = enterSubscriptions.Sum(kv => kv.Value.Count);
            var exitTotal = exitSubscriptions.Sum(kv => kv.Value.Count);
            GameLogger.Log("SCENE_REGISTRY", $"SceneSubscriptionRegistry initialized: {enterSubscriptions.Count} enter-scene(s) ({enterTotal} sub), {exitSubscriptions.Count} exit-scene(s) ({exitTotal} sub)");
        }

        private static Dictionary<string, string> BuildTemplateIndex()
        {
            var index = new Dictionary<string, string>();
            var mods = ModManager.Instance.Mods;
            if (mods == null)
                return index;

            foreach (var kvp in mods)
            {
                var provider = kvp.Value.FileProvider;
                foreach (var entry in provider.ListFiles("wrappers"))
                {
                    if (!entry.EndsWith(".wrappertmpl", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = System.IO.Path.GetFileNameWithoutExtension(entry.Replace('\\', '/').Split('/')[^1]);
                    if (index.ContainsKey(name))
                        continue;

                    var content = provider.ReadFileText(entry);
                    if (content != null)
                        index[name] = content;
                }
            }

            return index;
        }

        private static void CollectFromMods()
        {
            var mods = ModManager.Instance.Mods;
            if (mods == null)
            {
                GameLogger.Log("SCENE_REGISTRY", "CollectFromMods: no mods loaded", LogLevel.Error);
                return;
            }

            var templateIndex = BuildTemplateIndex();

            foreach (var kvp in mods)
            {
                var modId = kvp.Key;
                var modInfo = kvp.Value as ModInfo;
                if (modInfo?.Manifest?.Subscriptions == null)
                    continue;

                GameLogger.Log("SCENE_REGISTRY", $"CollectFromMods: mod '{modId}' has {modInfo.Manifest.Subscriptions.Count} subscription(s)", LogLevel.Debug);

                foreach (var entry in modInfo.Manifest.Subscriptions)
                {
                    if (string.IsNullOrEmpty(entry.Scene) || string.IsNullOrEmpty(entry.Template))
                    {
                        GameLogger.Log("SCENE_REGISTRY", $"CollectFromMods: skipping invalid entry in mod '{modId}'", LogLevel.Warning);
                        continue;
                    }

                    var content = modInfo.FileProvider.ReadFileText($"wrappers/{entry.Template}.wrappertmpl");
                    if (content == null && !templateIndex.TryGetValue(entry.Template, out content))
                    {
                        GameLogger.Log("SCENE_REGISTRY", $"CollectFromMods: template '{entry.Template}' not found in mod '{modId}' or any other mod", LogLevel.Error);
                        continue;
                    }

                    string codeContent = null;
                    if (!string.IsNullOrEmpty(entry.Code))
                    {
                        var codePath = $"subscriptions/{entry.Code}.cs";
                        codeContent = modInfo.FileProvider.ReadFileText(codePath);
                        if (codeContent == null)
                        {
                            GameLogger.Log("SCENE_REGISTRY", $"CollectFromMods: code file '{codePath}' not found in mod '{modId}'", LogLevel.Error);
                            continue;
                        }
                    }

                    var trigger = string.IsNullOrEmpty(entry.Trigger) ? "on_enter" : entry.Trigger;

                    var info = new SubscriptionInfo
                    {
                        ModId = modId,
                        Scene = entry.Scene,
                        TemplateName = entry.Template,
                        TemplateContent = content,
                        CodeName = entry.Code,
                        CodeContent = codeContent,
                        Priority = entry.Priority,
                        Trigger = trigger
                    };

                    var targetDict = trigger == "on_exit" ? exitSubscriptions : enterSubscriptions;

                    if (!targetDict.TryGetValue(entry.Scene, out var list))
                    {
                        list = new List<SubscriptionInfo>();
                        targetDict[entry.Scene] = list;
                    }
                    list.Add(info);

                    var codeDesc = string.IsNullOrEmpty(entry.Code) ? "" : $" code:{entry.Code}";
                    GameLogger.Log("SCENE_REGISTRY", $"CollectFromMods: registered [{trigger}] '{modId}/{entry.Template}'{codeDesc} -> scene '{entry.Scene}' (p:{entry.Priority})", LogLevel.Debug);
                }
            }
        }

        /// <summary>
        /// Compiles and executes all "on_enter" subscriptions for the given scene.
        /// Each subscription's template is compiled (with user code substitution via
        /// <c>{{USER_CODE}}</c>) by <see cref="ModCompiler"/>, then the resulting
        /// <c>SceneBuilderScript.Run(Node)</c> method is invoked with the scene root
        /// via a deferred call to ensure the scene tree is fully available.
        /// </summary>
        /// <param name="sceneName">The scene identifier to run subscriptions for.</param>
        /// <param name="sceneRoot">The root node of the target scene.</param>
        public static void RunSubscriptions(string sceneName, Node sceneRoot)
        {
            Initialize();
            ExecuteSubscriptions(enterSubscriptions, sceneName, sceneRoot, "on_enter");
        }

        /// <summary>
        /// Compiles and executes all "on_exit" subscriptions for the given scene,
        /// following the same pattern as <see cref="RunSubscriptions"/> but targeting
        /// the exit trigger.
        /// </summary>
        /// <param name="sceneName">The scene identifier to run exit subscriptions for.</param>
        /// <param name="sceneRoot">The root node of the scene being exited.</param>
        public static void RunExitSubscriptions(string sceneName, Node sceneRoot)
        {
            Initialize();
            ExecuteSubscriptions(exitSubscriptions, sceneName, sceneRoot, "on_exit");
        }

        private static void ExecuteSubscriptions(Dictionary<string, List<SubscriptionInfo>> dict, string sceneName, Node sceneRoot, string trigger)
        {
            if (!dict.TryGetValue(sceneName, out var list) || list.Count == 0)
                return;

            GameLogger.Log("SCENE_REGISTRY", $"RunSubscriptions [{trigger}]: scene '{sceneName}' -> {list.Count} subscription(s)", LogLevel.Debug);

            var sorted = list.OrderBy(s => s.Priority).ToList();

            foreach (var sub in sorted)
            {
                CompileResult compileResult;

                var codeKey = string.IsNullOrEmpty(sub.CodeName) ? "" : $"_{sub.CodeName}";
                var cacheKey = $"{sub.ModId}_{sub.TemplateName}{codeKey}";

                var sourceCode = sub.TemplateContent;
                if (!string.IsNullOrEmpty(sub.CodeContent))
                    sourceCode = sourceCode.Replace("{{USER_CODE}}", sub.CodeContent);

                if (compiledCache.TryGetValue(cacheKey, out var cached))
                {
                    compileResult = cached;
                    GameLogger.Log("SCENE_REGISTRY", $"RunSubscriptions [{trigger}]: using cached compilation for '{cacheKey}'", LogLevel.Debug);
                }
                else
                {
                    GameLogger.Log("SCENE_REGISTRY", $"RunSubscriptions [{trigger}]: compiling '{cacheKey}' ({sourceCode.Length} chars)", LogLevel.Debug);

                    compileResult = ModCompiler.CompileString(sourceCode, cacheKey);
                    if (compileResult.Success)
                    {
                        compiledCache[cacheKey] = compileResult;
                        GameLogger.Log("SCENE_REGISTRY", $"RunSubscriptions [{trigger}]: compilation OK for '{cacheKey}'", LogLevel.Debug);
                    }
                }

                if (!compileResult.Success)
                {
                    GameLogger.Log("SCENE_REGISTRY", $"RunSubscriptions [{trigger}]: compilation FAILED for '{cacheKey}': {string.Join("; ", compileResult.Errors ?? Enumerable.Empty<string>())}", LogLevel.Error);
                    continue;
                }

                try
                {
                    var type = compileResult.Assembly.GetType("SceneBuilderScript");
                    var method = type?.GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (method != null)
                    {
                        var rootCapture = sceneRoot;
                        Callable.From(() =>
                        {
                            if (GodotObject.IsInstanceValid(rootCapture))
                                method.Invoke(null, new object[] { rootCapture });
                        }).CallDeferred();
                    }
                    GameLogger.Log("SCENE_REGISTRY", $"RunSubscriptions [{trigger}]: enqueued '{cacheKey}' on scene '{sceneName}'", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("SCENE_REGISTRY", $"RunSubscriptions [{trigger}]: runtime error for '{cacheKey}': {ex.InnerException?.Message ?? ex.Message}", LogLevel.Error);
                }
            }
        }
    }
}
