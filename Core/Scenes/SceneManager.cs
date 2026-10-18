using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Actions;
using Cthangover.Core.Audio;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Central orchestrator for visual novel scene progression. Composes and schedules
    /// executable events from C# attributes and .scenario files, applies scene defaults
    /// (random background, ambient audio), and routes to native Godot scenes for menus
    /// and to mod-registered scene routes (see <see cref="SceneRouteRegistry"/>).
    /// Manages the <see cref="ExecutableMainEventChain"/> queue and
    /// drives scene enter/exit lifecycle events consumed by mod GUI and gameplay hooks.
    /// </summary>
    public partial class SceneManager : Node
    {
        private Dictionary<string, List<SceneDefinition>> allScenes;
        private string currentSceneName;
        private bool isInitialized;

        private IExecutableEventChain eventChain;
        private SceneEventController eventController;
        private IDialogBox dialogBox;

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

            var allScenarios = ScenarioDefs.CollectDefinitions();
            if (allScenarios == null || !allScenarios.TryGetValue(sceneName, out var scenarios))
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
            var toolBox = SceneContextNode.FindNode<Node>("TopView") as ISaveIconHost;
            toolBox?.UpdateSaveIconVisibility();
        }

        private void FilterCompletedScenarios(List<SceneEventInfo> composedEvents, Dictionary<SceneEventInfo, SceneCandidateReport> reports)
        {
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null)
                return;

            var beforeCount = composedEvents.Count;
            for (var i = composedEvents.Count - 1; i >= 0; i--)
            {
                if (!IsScenarioCompleted(composedEvents[i], runtime))
                    continue;
                reports[composedEvents[i]].Outcome = SceneCandidateOutcome.Completed;
                composedEvents.RemoveAt(i);
            }

            if (beforeCount != composedEvents.Count)
                GameLogger.Log("SCENE", $"SwitchScene: filtered {beforeCount - composedEvents.Count} completed scenario(s), {composedEvents.Count} remaining");
        }

        /// <summary>
        /// Checks whether a one-run scenario has already been completed in
        /// the current game. Consults both completion sources:
        /// <see cref="RuntimeData.CompletedScenarioIds"/> (persistent
        /// per-game history, updated as dialogs finish) and
        /// <see cref="RuntimeData.LoadedCompletedScenarioIds"/> (the raw
        /// block list restored from the save, valid until
        /// <see cref="RuntimeData.ClearLoadState"/>).
        /// </summary>
        private static bool IsScenarioCompleted(SceneEventInfo info, RuntimeData runtime)
        {
            if (info.Id != null && runtime.CompletedScenarioIds.Contains(info.Id))
                return true;

            return runtime.LoadedCompletedScenarioIds != null && info.Id != null
                && runtime.LoadedCompletedScenarioIds.Contains(info.Id);
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

        /// <summary>Runs the synchronous boot pipeline on non-launcher entries; launcher boots invoke the same phases through <see cref="Boot"/>.</summary>
        public override void _Ready()
        {
            if (!BootController.IsLauncherBoot)
                Boot.RunSync(this);
        }

        /// <summary>
        /// Implements the "Scenes" boot phase: initializes the <see cref="ModManager"/>,
        /// <see cref="SceneEventRegistry"/>, collects scene definitions from all loaded
        /// mods, fills the UI and main menu registries, notifies mod initializers and
        /// loads mod settings. The only normal caller is <see cref="Boot"/> (both
        /// pipeline branches invoke it from the same phase); the method is idempotent
        /// (<c>isInitialized</c>), so calls from outside <see cref="Boot"/> once boot
        /// has finished are no-ops.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
                return;

            ModManager.Instance.Initialize();
            AppServices.Current.SceneEvents.EnsureBuilt();
            allScenes = AppServices.Current.SceneDefs.CollectAll();
            AppServices.Current.Ui.EnsureBuilt();
            AppServices.Current.Menus.EnsureBuilt();
            ModInitializerRegistry.NotifyResourcesReady();
            ModSettingsRegistry.LoadAll();
            isInitialized = true;

            ModEventBus.Subscribe<SceneEnteredEvent>(e => ApplySceneDefaults(), priority: 0);
            ModEventBus.Subscribe<SceneEnteredEvent>(e => PlaySceneAmbient(), priority: 5);
            ModEventBus.Subscribe<DialogEndedEvent>(_ =>
            {
                if (IsTestPlayActive)
                {
                    GameLogger.Log("SCENE", "TEST-PLAY OnDialogEnd: dialog finished");
                    IsTestPlayActive = false;
                }
            });

            GameLogger.Log("SCENE", $"SceneManager initialized with {allScenes?.Count ?? 0} scene(s)");
        }

        /// <summary>
        /// Re-collects the logical scene table from the (re)loaded mod set
        /// without re-running the one-shot boot subscriptions. Called by
        /// <see cref="Mods.ModManager.Reload"/>; a no-op when the scene context
        /// is not available (pure test environments).
        /// </summary>
        public static void RescanScenes()
        {
            SceneContextNode.FindNode<SceneManager>("SceneManager")?.RescanSceneTable();
        }

        private void RescanSceneTable()
        {
            allScenes = AppServices.Current.SceneDefs.CollectAll();
            GameLogger.Log("SCENE", $"SceneManager rescanned: {allScenes?.Count ?? 0} scene(s)");
        }

        /// <summary>
        /// Core scene-switching pipeline. Clears static lights, resolves node references,
        /// freezes UI activity, then composes and sorts executable events for the target
        /// scene. Events come from C# classes registered via <see cref="SceneEventRegistry"/>
        /// and from .scenario files in mods. Conditions are evaluated via
        /// <see cref="ScenarioCondition.Evaluate"/>, valid events are instantiated and
        /// enqueued in the <see cref="ExecutableMainEventChain"/>. If no events exist,
        /// falls back to the scene's <see cref="SceneDefinition.DefaultScenario"/>.
        /// Godot-native scenes (mainmenu) and mod scene routes are routed through <see cref="GodotSceneService"/>.
        /// Publishes enter/exit lifecycle events consumed by mod hooks,
        /// applies a random default background, and updates music/ambient.
        /// </summary>
        /// <param name="sceneName">The identifier of the target scene.</param>
        public void SwitchScene(string sceneName)
		{
			GameLogger.Log("SCENE", $"SwitchScene: ENTER sceneName='{sceneName}', currentSceneName='{currentSceneName}'");
			if (!isInitialized)
				Initialize();
            
            LightingBridge.ClearSceneLights();
            
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

            if (AppServices.Current.SceneRoutes.TryGetRoute(sceneName, out var route))
            {
                var routeService = GetNode<GodotSceneService>("/root/GodotSceneService");
                routeService?.SwitchRoute(route);
                return;
            }

            if (IsGodotScene(sceneName))
            {
                var sceneService = GetNode<GodotSceneService>("/root/GodotSceneService");
                sceneService?.SwitchScene(GodotSceneType.MainMenu);
                return;
            }

            if (!string.IsNullOrEmpty(currentSceneName))
            {
                ModEventBus.Publish(new SceneExitedEvent { SceneName = currentSceneName });
            }

            GameLogger.Log("SCENE", $"SwitchScene: '{currentSceneName}' -> '{sceneName}'");

            ResolveReferences();

            FreezeActivity();
            ClearCurrentEvents();

            var reportList = new List<SceneCandidateReport>();
            var shadowed = new List<SceneCandidateReport>();
            try
            {
                var composedEvents = ComposeEvents(sceneName, shadowed);
                var reports = new Dictionary<SceneEventInfo, SceneCandidateReport>();
                foreach (var info in composedEvents ?? new List<SceneEventInfo>())
                {
                    var report = NewReport(info, SceneCandidateOutcome.Queued);
                    reports[info] = report;
                    reportList.Add(report);
                }
                reportList.AddRange(shadowed);

                if (composedEvents == null || composedEvents.Count == 0)
                {
                    currentSceneName = sceneName;
                    ModEventBus.Publish(new SceneEnteredEvent
                    {
                        SceneName = sceneName,
                        Root = GetTree().CurrentScene,
                    });
                    if (!IsTestPlayActive)
                        TryLoadDefaultScenario();
                    return;
                }

                currentSceneName = sceneName;

                ModEventBus.Publish(new SceneEnteredEvent
                {
                    SceneName = sceneName,
                    Root = GetTree().CurrentScene,
                });

                FilterCompletedScenarios(composedEvents, reports);

                var pendingEvents = new List<IExecutableEvent>();
                foreach (var eventInfo in composedEvents)
                {
                    try
                    {
                        if (!ScenarioCondition.Evaluate(eventInfo.Condition))
                        {
                            reports[eventInfo].Outcome = SceneCandidateOutcome.ConditionFalse;
                            GameLogger.Log("SCENE", $"SwitchScene: skipped '{eventInfo.Id}' (condition not met: {eventInfo.Condition})", LogLevel.Warning);
                            continue;
                        }

                        var instance = CreateEventInstance(eventInfo);
                        if (instance != null)
                        {
                            pendingEvents.Add(instance);
                            GameLogger.Log("SCENE", $"SwitchScene: created event '{eventInfo.Id}'");
                        }
                        else
                        {
                            reports[eventInfo].Outcome = SceneCandidateOutcome.Failed;
                        }
                    }
                    catch (Exception ex)
                    {
                        reports[eventInfo].Outcome = SceneCandidateOutcome.Failed;
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

                UpdateSaveIcon();

                GameData.Instance?.Runtime.ClearLoadState();
            }
            finally
            {
                UnfreezeActivity();
                if (!IsTestPlayActive)
                    ModEventBus.Publish(new SceneComposedEvent { SceneName = sceneName, Candidates = reportList });
            }
        }

        private static SceneCandidateReport NewReport(SceneEventInfo info, SceneCandidateOutcome outcome)
        {
            return new SceneCandidateReport
            {
                Id = info.Id,
                ModId = info.ModId,
                ScenarioPath = info.ScenarioPath,
                Priority = info.Priority,
                Condition = info.Condition,
                IsOneRun = info.IsOneRun,
                Outcome = outcome,
            };
        }

        private List<SceneEventInfo> ComposeEvents(string sceneName, List<SceneCandidateReport> shadowed)
        {
            if (IsTestPlayActive && !string.IsNullOrEmpty(TestScenarioText))
            {
                var factory = AppServices.Current.SceneEvents.ScenarioEventFactory;
                if (factory == null)
                {
                    GameLogger.Log("SCENE", "ComposeEvents: no ScenarioEventFactory registered (dialog_base mod not loaded)", LogLevel.Error);
                }
                else
                {
                    var evt = factory.CreateFromText(TestScenarioText);
                    evt.IsOneRun = true;
                    eventChain?.AddEvent(evt);
                }
                TestScenarioText = null;
                return new List<SceneEventInfo>();
            }

            var allEvents = new List<SceneEventInfo>();
            var seenIds = new HashSet<string>();

            // 1. Collect C# events from registry
            var registered = AppServices.Current.SceneEvents.GetEventsSorted(sceneName);
            foreach (var evt in registered)
            {
                if (seenIds.Add(evt.Id))
                    allEvents.Add(evt);
                else
                    shadowed.Add(NewReport(evt, SceneCandidateOutcome.Shadowed));
            }

            // 2. Collect .scenario file events from mods
            var scenarioDefs = ScenarioDefs.CollectDefinitions();
            if (scenarioDefs != null && scenarioDefs.TryGetValue(sceneName, out var scenarios))
            {
                foreach (var scenario in scenarios)
                {
                    var id = scenario.Name;
                    var info = new SceneEventInfo
                    {
                        Id = id,
                        ScenarioPath = scenario.FilePath,
                        ModId = scenario.ModId,
                        Priority = scenario.Priority,
                        After = scenario.After,
                        Condition = scenario.Condition,
                        LightUseTime = scenario.LightUseTime,
                        IsOneRun = scenario.IsOneRun,
                    };

                    if (seenIds.Add(id))
                        allEvents.Add(info);
                    else
                        shadowed.Add(NewReport(info, SceneCandidateOutcome.Shadowed));
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

        /// <summary>
        /// True when the name is a built-in Godot scene route
        /// (<c>mainmenu</c>/<c>menu</c>) rather than a mod-provided route or a
        /// mod-defined logical scene. Exposed so tooling — the validator mod —
        /// can classify scenario scene references with the same rules as the
        /// runtime.
        /// </summary>
        public static bool IsNativeSceneName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            var lower = sceneName.ToLowerInvariant();
            return lower == "mainmenu" || lower == "menu";
        }

        private static bool IsGodotScene(string sceneName) => IsNativeSceneName(sceneName);

        /// <summary>
        /// Checks whether a logical scene name can be switched to: a Godot-native
        /// route (mainmenu/menu), a scene route registered by a mod through
        /// <see cref="SceneRouteRegistry"/>, or a scenario scene collected from the
        /// mods. Tools such as <c>SceneSwitcher</c> use it to reject typos with a
        /// non-zero exit code instead of silently running an empty scene and
        /// reporting success.
        /// </summary>
        public bool HasScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;
            if (!isInitialized)
                Initialize();
            if (IsGodotScene(sceneName))
                return true;
            if (AppServices.Current.SceneRoutes.TryGetRoute(sceneName, out _))
                return true;
            return allScenes != null && allScenes.ContainsKey(sceneName);
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
			LightingBridge.SetupBackgroundMaps(depthTex, albedoTex);
        }

        private IBackgroundView GetViewBox()
        {
            var sceneRoot = GetTree()?.CurrentScene;
            if (sceneRoot == null)
                return null;

            var scene = sceneRoot.FindChild("Scene", false, false);
            if (scene == null)
                return null;

            return scene.GetNodeOrNull<Node>("ViewBox") as IBackgroundView;
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

            var factory = AppServices.Current.SceneEvents.ScenarioEventFactory;
            if (factory == null)
            {
                GameLogger.Log("SCENE", "SwitchScene: no ScenarioEventFactory registered (dialog_base mod not loaded)", LogLevel.Error);
                return;
            }

            eventChain?.AddEvent(factory.CreateFromText(text));

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
                var audioService = GetNodeOrNull<Node>("/root/AudioService") as IAudioService;
                audioService?.StopAmbient();
                return;
            }

            var audio = GetNodeOrNull<Node>("/root/AudioService") as IAudioService;
            audio?.PlayAmbient(ambientId);
        }

        private IExecutableEvent CreateEventInstance(SceneEventInfo info)
        {
            if (!string.IsNullOrEmpty(info.ClassName))
            {
                var type = Type.GetType(info.ClassName);
                if (type == null)
                {
                    type = Reflections.LoadedAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.FullName == info.ClassName);
                }

                if (type == null)
                {
                    GameLogger.Log("SCENE", $"CreateEventInstance: type not found '{info.ClassName}'", LogLevel.Error);
                    return null;
                }

                if (!typeof(IExecutableEvent).IsAssignableFrom(type))
                {
                    GameLogger.Log("SCENE", $"CreateEventInstance: '{info.ClassName}' is not an IExecutableEvent");
                    return null;
                }

                var instance = (IExecutableEvent)Activator.CreateInstance(type);
                if (instance is IConditionedEvent ce && !string.IsNullOrEmpty(info.Condition))
                    ce.Condition = info.Condition;
                return instance;
            }

            if (!string.IsNullOrEmpty(info.ScenarioPath))
            {
                var scenarioText = string.IsNullOrEmpty(info.ModId)
                    ? ReadScenarioText(info.ScenarioPath)
                    : ScenarioDefs.ReadText(info.ModId, info.ScenarioPath);
                if (scenarioText == null)
                {
                    GameLogger.Log("SCENE", $"CreateEventInstance: cannot read scenario '{info.ScenarioPath}'", LogLevel.Error);
                    return null;
                }

                var factory = AppServices.Current.SceneEvents.ScenarioEventFactory;
                if (factory == null)
                {
                    GameLogger.Log("SCENE", $"CreateEventInstance: no ScenarioEventFactory registered (dialog_base mod not loaded), event '{info.Id}'", LogLevel.Error);
                    return null;
                }

                return factory.CreateFromDefinition(scenarioText, info.Id, info.Condition, info.LightUseTime, info.IsOneRun, info.ModId, info.ScenarioPath);
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

            if (eventChain == null || eventChain is not GodotObject chainNode || !IsInstanceValid(chainNode))
                eventChain = GetTree()?.GetFirstNodeInGroup("main_event_chain") as IExecutableEventChain;

            if (dialogBox == null || dialogBox is not GodotObject dialogNode || !IsInstanceValid(dialogNode))
            {
                var root = GetTree()?.Root;
                if (root != null)
                    dialogBox = SceneContextNode.FindNode<Node>("DialogBox") as IDialogBox;
            }
        }
    }
}
