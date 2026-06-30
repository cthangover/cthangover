using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.UI.Lights;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.UI.View;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Central orchestrator for visual novel scene progression. Composes and schedules
    /// executable events from C# attributes and .scenario files, applies scene defaults
    /// (random background, ambient audio), and routes to native Godot scenes for menus
    /// and battles. Manages the <see cref="ExecutableMainEventChain"/> queue and
    /// coordinates with <see cref="SceneSubscriptionRegistry"/> for enter/exit hooks.
    /// </summary>
    public partial class SceneManager : Node, IOnDialogEndEvent
    {
        private Dictionary<string, List<SceneDefinition>> allScenes;
        private string currentSceneName;
        private bool isInitialized;

        private ExecutableMainEventChain eventChain;
        private SceneEventController eventController;
        private DialogBox dialogBox;

        /// <summary>
        /// Gets the name of the currently active scene as tracked by this manager.
        /// </summary>
        public string CurrentSceneName => currentSceneName;

        /// <summary>
        /// Statically retrieves the current scene name by locating the
        /// <see cref="SceneManager"/> singleton via <see cref="SceneContextNode"/>.
        /// Returns <c>null</c> if the manager has not been instantiated.
        /// </summary>
        public static string GetCurrentSceneName()
        {
            var instance = SceneContextNode.FindNode<SceneManager>("SceneManager");
            return instance?.CurrentSceneName;
        }

        /// <summary>
        /// Checks whether saving is permitted in the currently active scene by inspecting
        /// all <see cref="ScenarioDefinition"/> entries associated with it. Returns
        /// <c>true</c> if any scenario for the scene has <c>SaveAllowed</c> enabled.
        /// </summary>
        public static bool IsSaveAllowedForCurrentScene()
        {
            var instance = SceneContextNode.FindNode<SceneManager>("SceneManager");
            if (instance == null)
                return false;

            var sceneName = instance.CurrentSceneName;
            if (string.IsNullOrEmpty(sceneName))
                return false;

            var allScenarios = ModManager.Instance.CollectScenarioDefinitions();
            if (!allScenarios.TryGetValue(sceneName, out var scenarios))
                return false;

            foreach (var def in scenarios)
            {
                if (def.SaveAllowed)
                    return true;
            }
            return false;
        }

        private void UpdateSaveIcon()
        {
            var toolBox = SceneContextNode.FindNode<ToolBox>("TopView");
            toolBox?.UpdateSaveIconVisibility();
        }

        private void FilterCompletedScenarios(List<SceneEventInfo> composedEvents)
        {
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null || runtime.LoadedCompletedScenarioIds == null || runtime.LoadedCompletedScenarioIds.Count == 0)
                return;

            composedEvents.RemoveAll(e => runtime.LoadedCompletedScenarioIds.Contains(e.Id));
            GameLogger.Log("SCENE", $"SwitchScene: filtered {runtime.LoadedCompletedScenarioIds.Count} completed scenario(s), {composedEvents.Count} remaining");
        }

        /// <summary>
        /// Gets or sets the name of a scene queued for deferred transition, used when a
        /// scene switch must be delayed (e.g. awaiting dialog completion).
        /// </summary>
        public string PendingSceneName { get; set; }

        /// <summary>
        /// Gets or sets whether test-play mode is active. When enabled,
        /// <see cref="SwitchScene"/> redirects non-menu targets to the main menu unless
        /// <see cref="TestScenarioText"/> is populated.
        /// </summary>
        public static bool IsTestPlayActive { get; set; }

        /// <summary>
        /// Gets or sets the scenario text to execute in test-play mode. Once consumed
        /// by <see cref="ComposeEvents"/>, it is cleared.
        /// </summary>
        public static string TestScenarioText { get; set; }

        public override void _Ready()
        {
            Initialize();
            var ec = GetNodeOrNull<SceneEventController>("/root/EventController");
            ec?.AddDialogEndEventListener(this);
        }

        /// <summary>
        /// Initializes the <see cref="ModManager"/>, <see cref="SceneEventRegistry"/>,
        /// and collects scene definitions from all loaded mods. Safe to call
        /// multiple times; subsequent calls are no-ops.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
                return;

            ModManager.Instance.Initialize();
            SceneEventRegistry.Initialize();
            allScenes = ModManager.Instance.CollectSceneDefinitions();
            isInitialized = true;

            GameLogger.Log("SCENE", $"SceneManager initialized with {allScenes?.Count ?? 0} scene(s)");
        }

        int IEventPriority.Priority => 0;

        void IOnDialogEndEvent.OnDialogEnd(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent)
        {
            if (IsTestPlayActive)
            {
                GameLogger.Log("SCENE", "TEST-PLAY OnDialogEnd: dialog finished");
                IsTestPlayActive = false;
            }
        }

        /// <summary>
        /// Core scene-switching pipeline. Clears static lights, resolves node references,
        /// freezes UI activity, then composes and sorts executable events for the target
        /// scene. Events come from C# classes registered via <see cref="SceneEventRegistry"/>
        /// and from .scenario files in mods. Conditions are evaluated via
        /// <see cref="ScenarioCondition.Evaluate"/>, valid events are instantiated and
        /// enqueued in the <see cref="ExecutableMainEventChain"/>. If no events exist,
        /// falls back to the scene's <see cref="SceneDefinition.DefaultScenario"/>.
        /// Godot-native scenes (mainmenu, battle) are routed through <see cref="GodotSceneService"/>.
        /// Runs enter/exit subscriptions from <see cref="SceneSubscriptionRegistry"/>,
        /// applies a random default background, and updates music/ambient.
        /// </summary>
        /// <param name="sceneName">The identifier of the target scene.</param>
        public void SwitchScene(string sceneName)
		{
			GameLogger.Log("SCENE", $"SwitchScene: ENTER sceneName='{sceneName}', currentSceneName='{currentSceneName}'");
			if (!isInitialized)
				Initialize();
            
            var lightController = SceneContextNode.FindNode<UiLightController>("Lights");
            if (lightController != null)
            {
                lightController.ClearStaticLights();
                lightController.SetupDepthMap(null);
                lightController.SetupAlbedoMap(null);
            }
            
            if (string.IsNullOrEmpty(sceneName))
            {
                GameLogger.Log("SCENE", "SwitchScene: sceneName is null or empty", LogLevel.Error);
                return;
            }

            if (IsTestPlayActive)
            {
                var lowerTest = sceneName.ToLowerInvariant();
                if (lowerTest == "mainmenu" || lowerTest == "menu")
                {
                    IsTestPlayActive = false;
                }
                else if (string.IsNullOrEmpty(TestScenarioText))
                {
                    GameLogger.Log("SCENE", $"SwitchScene: test play redirect '{sceneName}' -> 'mainmenu'");
                    sceneName = "mainmenu";
                }
            }

            if (IsGodotScene(sceneName))
            {
                var sceneService = GetNode<GodotSceneService>("/root/GodotSceneService");
                if (sceneService != null)
                {
                    var lower = sceneName.ToLowerInvariant();
                    var sceneType = (lower == "mainmenu" || lower == "menu") ? GodotSceneType.MainMenu : GodotSceneType.Battle;
                    sceneService.SwitchScene(sceneType);
                }
                return;
            }

            if (!string.IsNullOrEmpty(currentSceneName))
                SceneSubscriptionRegistry.RunExitSubscriptions(currentSceneName, GetTree().CurrentScene);

            GameLogger.Log("SCENE", $"SwitchScene: '{currentSceneName}' -> '{sceneName}'");

            ResolveReferences();

            FreezeActivity();
            ClearCurrentEvents();

            try
            {
                var composedEvents = ComposeEvents(sceneName);
                if (composedEvents == null || composedEvents.Count == 0)
                {
                    currentSceneName = sceneName;
                    ApplySceneDefaults();
                    if (!IsTestPlayActive)
                        TryLoadDefaultScenario();
                    return;
                }

                currentSceneName = sceneName;

                ApplySceneDefaults();

                FilterCompletedScenarios(composedEvents);

                var pendingEvents = new List<ExecutableEvent>();
                foreach (var eventInfo in composedEvents)
                {
                    try
                    {
                        if (!ScenarioCondition.Evaluate(eventInfo.Condition))
                        {
                            GameLogger.Log("SCENE", $"SwitchScene: skipped '{eventInfo.Id}' (condition not met: {eventInfo.Condition})", LogLevel.Warning);
                            continue;
                        }

                        var instance = CreateEventInstance(eventInfo);
                        if (instance != null)
                        {
                            pendingEvents.Add(instance);
                            GameLogger.Log("SCENE", $"SwitchScene: created event '{eventInfo.Id}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("SCENE", $"SwitchScene: failed to create event '{eventInfo.Id}': {ex.Message}", LogLevel.Error);
                    }
                }
                
                GameLogger.Log("SCENE", $"SwitchScene: adding {pendingEvents.Count} pending event(s) to chain");

                foreach (var instance in pendingEvents)
                {
                    if (eventChain == null)
                    {
                        GameLogger.Log("SCENE", $"  ERROR: eventChain is null, skipping event '{instance.GetType().Name}'", LogLevel.Error);
                        continue;
                    }
                    eventChain.AddEvent(instance);
                }

                foreach (var musicPlayer in GetTree().GetNodesInGroup("music_player"))
                {
                    if (musicPlayer is Audio.MusicPlayerBehaviour cast)
                        cast.UpdateMusicType(sceneName);
                }

                PlaySceneAmbient();
                SceneSubscriptionRegistry.RunSubscriptions(sceneName, GetTree().CurrentScene);

                UpdateSaveIcon();

                GameData.Instance?.Runtime.ClearLoadState();
            }
            finally
            {
                UnfreezeActivity();
            }
        }

        private List<SceneEventInfo> ComposeEvents(string sceneName)
        {
            if (IsTestPlayActive && !string.IsNullOrEmpty(TestScenarioText))
            {
                var evt = new ScenarioEvent { ScenarioText = TestScenarioText, IsOneRun = true };
                TestScenarioText = null;
                eventChain?.AddEvent(evt);
                return new List<SceneEventInfo>();
            }

            var allEvents = new List<SceneEventInfo>();
            var seenIds = new HashSet<string>();

            // 1. Collect C# events from registry
            var registered = SceneEventRegistry.GetEvents(sceneName);
            foreach (var evt in registered)
            {
                if (seenIds.Add(evt.Id))
                    allEvents.Add(evt);
            }

            // 2. Collect .scenario file events from mods
            var scenarioDefs = ModManager.Instance.CollectScenarioDefinitions();
            if (scenarioDefs.TryGetValue(sceneName, out var scenarios))
            {
                foreach (var scenario in scenarios)
                {
                    var id = scenario.Name;
                    if (!seenIds.Add(id))
                        continue;

                    allEvents.Add(new SceneEventInfo
                    {
                        Id = id,
                        ScenarioPath = scenario.FilePath,
                        Priority = scenario.Priority,
                        After = scenario.After,
                        Condition = scenario.Condition,
                        LightUseTime = scenario.LightUseTime,
                        IsOneRun = scenario.IsOneRun,
                    });
                }
            }

            // 3. Sort by priority + topological sort for 'after' dependencies
            allEvents = SortEvents(allEvents);
            
            GameLogger.Log("SCENE", $"ComposeEvents '{sceneName}': {allEvents.Count} event(s)");

            return allEvents;
        }

        private static List<SceneEventInfo> SortEvents(List<SceneEventInfo> events)
        {
            var visited = new HashSet<string>();
            var result = new List<SceneEventInfo>();
            var visiting = new HashSet<string>();

            void Visit(SceneEventInfo info)
            {
                if (info.Id == null)
                {
                    if (!result.Contains(info))
                        result.Add(info);
                    return;
                }

                if (visited.Contains(info.Id))
                    return;

                if (visiting.Contains(info.Id))
                {
                    GameLogger.Log("SCENE", $"Circular dependency detected: {info.Id}", LogLevel.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(info.After))
                {
                    var after = events.FirstOrDefault(e => e.Id == info.After);
                    if (after != null)
                    {
                        visiting.Add(info.Id);
                        Visit(after);
                        visiting.Remove(info.Id);
                    }
                }

                visited.Add(info.Id);
                result.Add(info);
            }

            foreach (var info in events.OrderBy(e => e.Priority).ThenBy(e => e.Id))
                Visit(info);

            return result;
        }

        private static bool IsGodotScene(string sceneName)
        {
            var lower = sceneName.ToLowerInvariant();
            return lower == "mainmenu" || lower == "menu" || lower == "battle";
        }

        private static readonly Random _bgRandom = new();

        private void ApplySceneDefaults()
        {
            var defs = allScenes?.GetValueOrDefault(currentSceneName);
            if (defs == null || defs.Count == 0)
                return;

            var def = defs[0];
            if (def.DefaultBackground != null && def.DefaultBackground.Count > 0)
            {
                var index = def.DefaultBackground.Count > 1
                    ? _bgRandom.Next(def.DefaultBackground.Count)
                    : 0;
                var bg = def.DefaultBackground[index];
                if (!string.IsNullOrEmpty(bg))
                {
                    SceneContextNode.LastBackgroundID = bg;
                    LoadDefaultBackground(bg);
                }
            }
        }

        private void LoadDefaultBackground(string backgroundId)
        {
            var texture = BackgroundFactory.Instance.Get(backgroundId);
            if (texture == null)
            {
                GameLogger.Log("SCENE", $"LoadDefaultBackground: failed to load '{backgroundId}'", LogLevel.Error);
                return;
            }

			var viewBox = GetViewBox();
			if (viewBox != null)
			{
				if (GodotSceneService.IsTransitioning)
					viewBox.SetBackgroundTexture(texture);
				else
					viewBox.TransitionBackground(texture);
			}

			var depthTex = BackgroundFactory.Instance.Get(backgroundId + "_depth");
			var albedoTex = BackgroundFactory.Instance.Get(backgroundId + "_albedo");
			var controller = UiLightController.Instance;
			controller?.SetupDepthMap(depthTex);
			controller?.SetupAlbedoMap(albedoTex);
        }

        private ViewBox GetViewBox()
        {
            var sceneRoot = GetTree()?.CurrentScene;
            if (sceneRoot == null)
                return null;

            var scene = sceneRoot.FindChild("Scene", false, false);
            if (scene == null)
                return null;

            return scene.GetNodeOrNull<ViewBox>("ViewBox");
        }

        private void TryLoadDefaultScenario()
        {
            var defs = allScenes?.GetValueOrDefault(currentSceneName);
            if (defs == null || defs.Count == 0)
                return;

            var path = defs[0].DefaultScenario;
            if (string.IsNullOrEmpty(path))
            {
                GameLogger.Log("SCENE", $"SwitchScene: no events and no defaultScenario for '{currentSceneName}'", LogLevel.Error);
                return;
            }

            var text = ReadScenarioText(path);
            if (text == null)
            {
                GameLogger.Log("SCENE", $"SwitchScene: defaultScenario '{path}' not found for '{currentSceneName}'", LogLevel.Error);
                return;
            }

            var evt = new ScenarioEvent { ScenarioText = text };
            eventChain?.AddEvent(evt);

            GameLogger.Log("SCENE", $"SwitchScene: loaded defaultScenario '{path}' for '{currentSceneName}'");
        }

        private void PlaySceneAmbient()
        {
            var defs = allScenes?.GetValueOrDefault(currentSceneName);
            if (defs == null || defs.Count == 0)
                return;

            var def = defs[0];
            var ambientId = def.DefaultAmbient;
            if (string.IsNullOrEmpty(ambientId))
            {
                var audioService = GetNodeOrNull<Audio.AudioService>("/root/AudioService");
                audioService?.StopAmbient();
                return;
            }

            var audio = GetNodeOrNull<Audio.AudioService>("/root/AudioService");
            audio?.PlayAmbient(ambientId);
        }

        private ExecutableEvent CreateEventInstance(SceneEventInfo info)
        {
            if (!string.IsNullOrEmpty(info.ClassName))
            {
                var type = Type.GetType(info.ClassName);
                if (type == null)
                {
                    type = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.FullName == info.ClassName);
                }

                if (type == null)
                {
                    GameLogger.Log("SCENE", $"CreateEventInstance: type not found '{info.ClassName}'", LogLevel.Error);
                    return null;
                }

                if (!typeof(ExecutableEvent).IsAssignableFrom(type))
                {
                    GameLogger.Log("SCENE", $"CreateEventInstance: '{info.ClassName}' is not an ExecutableEvent");
                    return null;
                }

                var instance = (ExecutableEvent)Activator.CreateInstance(type);
                if (instance is ScenarioEvent se && !string.IsNullOrEmpty(info.Condition))
                    se.Condition = info.Condition;
                return instance;
            }

            if (!string.IsNullOrEmpty(info.ScenarioPath))
            {
                var scenarioText = ReadScenarioText(info.ScenarioPath);
                if (scenarioText == null)
                {
                    GameLogger.Log("SCENE", $"CreateEventInstance: cannot read scenario '{info.ScenarioPath}'", LogLevel.Error);
                    return null;
                }

                return new ScenarioEvent
                {
                    ScenarioText = scenarioText,
                    ScenarioId = info.Id,
                    Condition = info.Condition,
                    LightUseTime = info.LightUseTime,
                    IsOneRun = info.IsOneRun,
                };
            }
            
            GameLogger.Log("SCENE", $"CreateEventInstance: no ClassName or ScenarioPath for '{info.Id}'");
            return null;
        }

        private static string ReadScenarioText(string filePath)
        {
            // Try reading through a mod provider first
            foreach (var kvp in ModRegistry.Instance.Mods)
            {
                var provider = kvp.Value.FileProvider;
                var text = provider.ReadFileText(filePath);
                if (text != null)
                    return text;
            }

            // Fallback to Godot's FileAccess
            var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            if (file != null)
            {
                var text = file.GetAsText();
                file.Close();
                return text;
            }

            return null;
        }

        private void ClearCurrentEvents()
        {
            eventChain?.ClearEvents();
        }

        private void FreezeActivity()
        {
            if (eventController != null)
                eventController.IsActive = false;
            if (dialogBox != null)
                dialogBox.IsActive = false;
        }

        private void UnfreezeActivity()
        {
            if (eventController != null)
                eventController.IsActive = true;
            if (dialogBox != null)
                dialogBox.IsActive = true;
        }

        private void ResolveReferences()
        {
            if (eventController == null || !IsInstanceValid(eventController))
                eventController = GetNodeOrNull<SceneEventController>("/root/EventController");

            if (eventChain == null || !IsInstanceValid(eventChain))
                eventChain = GetTree()?.GetFirstNodeInGroup("main_event_chain") as ExecutableMainEventChain;

            if (dialogBox == null || !IsInstanceValid(dialogBox))
            {
                var root = GetTree()?.Root;
                if (root != null)
                    dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");
            }
        }
    }
}
