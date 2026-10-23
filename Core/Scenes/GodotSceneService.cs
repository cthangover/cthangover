using System;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{
	/// <summary>
	/// Manages native Godot scene file (.tscn) transitions with a shader-based fade
	/// overlay. Handles scene routing for menus, mod scene routes and the main base scene.
	/// Orchestrates the full transition lifecycle: fade-out, synchronous scene change
	/// via <see cref="SceneTree.ChangeSceneToFile"/>, deferred shader patching, subscription
	/// execution, and fade-in. The static <see cref="IsTransitioning"/> flag is consumed
	/// by <see cref="SceneManager"/> to decide between instant and animated background
	/// swaps during scene entry.
	/// </summary>
	public partial class GodotSceneService : Node
	{
		private SceneManager sceneManager;
		private CanvasLayer transitionLayer;
		private TextureRect transitionOverlay;
		private ShaderMaterial transitionMaterial;
		private string pendingScenePath;
		private string pendingSubscriptionSceneName;
		private string currentGodotSceneName;
		private Tween fadeTween;

		/// <summary>
		/// Indicates whether a scene transition fade is currently in progress. When
		/// <c>true</c>, <see cref="SceneManager"/> applies background changes instantly
		/// rather than animating them, avoiding visual conflicts with the fade effect.
		/// </summary>
		public static bool IsTransitioning { get; private set; }

		/// <summary>
		/// Logical name of the Godot scene last switched to (a mod route such as
		/// <c>mods_hub</c>, or <c>mainmenu</c>). Lets a tool that temporarily
		/// leaves the screen (the scenario editor's play-test) come back to it.
		/// </summary>
		public string CurrentGodotSceneName => currentGodotSceneName;

		/// <summary>Enables always-on processing (transitions must run while the game is paused) and caches the <see cref="SceneManager"/> autoload.</summary>
		public override void _Ready()
		{
			base._Ready();
			ProcessMode = ProcessModeEnum.Always;
			sceneManager = GetNode<SceneManager>("/root/SceneManager");
		}

		/// <summary>
		/// Initiates loading a .tscn scene file. Notifies all music players in the
		/// "music_player" group when the target is a registered mod route, then
		/// delegates to <see cref="BeginSceneTransition"/> for the actual transition.
		/// Routes registered with fade enabled receive the shader transition;
		/// everything else switches immediately.
		/// </summary>
		/// <param name="nextScene">The resource path to the .tscn file to load.</param>
		public void LoadScene(string nextScene)
		{
			GameLogger.Log("SCENE", $"load scene '{nextScene}'...");

			var route = AppServices.Current.SceneRoutes.FindByScenePath(nextScene);
			if (route != null && !string.IsNullOrEmpty(route.MusicScene))
			{
				GameLogger.Log("AUDIO", $"GodotSceneService publishing route activation for '{route.LogicalName}'");
				ModEventBus.Publish(new SceneRouteActivatingEvent
				{
					LogicalName = route.LogicalName,
					ScenePath = nextScene,
					MusicScene = route.MusicScene,
				});
			}

			BeginSceneTransition(nextScene);
		}

		private void BeginSceneTransition(string nextScene)
		{
			pendingScenePath = nextScene;

			if (!ShouldFade(pendingScenePath))
			{
				PerformSceneSwitch();
				return;
			}

			IsTransitioning = true;

			var tree = GetTree();
			if (tree == null)
				return;

			var root = tree.Root;
			if (root == null)
				return;

			EnsureOverlay(root);

			transitionLayer.Visible = true;

			if (transitionMaterial != null)
			{
				transitionMaterial.SetShaderParameter("progress", 1f);

				KillFadeTween();

				fadeTween = tree.CreateTween();
				fadeTween.SetProcessMode(Tween.TweenProcessMode.Physics);
				fadeTween.TweenMethod(Callable.From<float>(OnShaderExitProgress), 1f, 0f, 0.25f);
				fadeTween.Finished += OnFadeOutComplete;
			}
		}

		private static bool ShouldFade(string scenePath)
		{
			var route = AppServices.Current.SceneRoutes.FindByScenePath(scenePath);
			return route != null && route.Fade;
		}

		private void EnsureOverlay(Node root)
		{
			if (transitionLayer != null && GodotObject.IsInstanceValid(transitionLayer))
				return;

			transitionLayer = new CanvasLayer
			{
				Name = "SceneTransitionLayer",
				Layer = 128
			};
			root.AddChild(transitionLayer);

			transitionOverlay = new TextureRect
			{
				Name = "SceneTransitionOverlay",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale
			};
			transitionOverlay.AnchorRight = 1f;
			transitionOverlay.AnchorBottom = 1f;
			transitionOverlay.GrowHorizontal = Control.GrowDirection.Both;
			transitionOverlay.GrowVertical = Control.GrowDirection.Both;
			transitionLayer.AddChild(transitionOverlay);

			var blackImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
			blackImage.Fill(new Color(0, 0, 0, 1));
			transitionOverlay.Texture = ImageTexture.CreateFromImage(blackImage);

			var shader = Shaders.Resolve("scene_transition");
			if (shader != null)
			{
				transitionMaterial = new ShaderMaterial();
				transitionMaterial.Shader = shader;
				transitionOverlay.Material = transitionMaterial;
			}
		}

		private void KillFadeTween()
		{
			if (fadeTween != null)
			{
				fadeTween.Kill();
				fadeTween = null;
			}
		}

		private void OnShaderExitProgress(float t)
		{
			if (transitionMaterial != null)
				transitionMaterial.SetShaderParameter("progress", t);
		}

		private void OnFadeOutComplete()
		{
			KillFadeTween();
			CallDeferred(nameof(PerformSceneSwitch));
		}

		private void PerformSceneSwitch()
		{
			var tree = GetTree();
			if (tree == null)
				return;

			GameLogger.Log("SCENE", $"PerformSceneSwitch: changing scene to '{pendingScenePath}'...");

			if (IsModScene(pendingScenePath) || TscnScenes.HasPatches(pendingScenePath))
			{
				var node = TscnScenes.LoadAndBuild(pendingScenePath);
				if (node == null)
				{
					GameLogger.Log("SCENE", $"PerformSceneSwitch: failed to build mod scene '{pendingScenePath}'", LogLevel.Error);
					return;
				}
				tree.ChangeSceneToNode(node);
			}
			else
			{
				tree.ChangeSceneToFile(pendingScenePath);
			}

			GameLogger.Log("SCENE", $"PerformSceneSwitch: scene changed to '{pendingScenePath}' OK");

			CallDeferred(nameof(PatchShaderMaterialsAfterLoad));
			CallDeferred(nameof(RunSubscriptionAfterLoad));

			if (ShouldFade(pendingScenePath))
				CallDeferred(nameof(StartFadeInAfterSwitch));
		}

		private void RunSubscriptionAfterLoad()
		{
			GameLogger.Log("SCENE", $"RunSubscriptionAfterLoad: scene='{pendingSubscriptionSceneName}'");
			if (string.IsNullOrEmpty(pendingSubscriptionSceneName))
				return;

			var tree = GetTree();
			if (tree == null)
				return;

			ModEventBus.Publish(new SceneEnteredEvent
			{
				SceneName = pendingSubscriptionSceneName,
				Root = tree.CurrentScene,
			});
			GameLogger.Log("SCENE", "RunSubscriptionAfterLoad: DONE");
		}

		private void StartFadeInAfterSwitch()
		{
			var tree = GetTree();
			if (tree == null)
				return;

			var root = tree.Root;
			if (root == null)
				return;

			EnsureOverlay(root);

			if (transitionMaterial == null)
				return;

			transitionLayer.Visible = true;
			transitionMaterial.SetShaderParameter("progress", 0f);

			KillFadeTween();

			fadeTween = tree.CreateTween();
			fadeTween.SetProcessMode(Tween.TweenProcessMode.Physics);
			fadeTween.TweenMethod(Callable.From<float>(OnShaderEnterProgress), 0f, 1f, 0.25f);
			fadeTween.Finished += OnFadeInComplete;
		}

		private void OnShaderEnterProgress(float t)
		{
			if (transitionMaterial != null)
				transitionMaterial.SetShaderParameter("progress", t);
		}

		private void OnFadeInComplete()
		{
			KillFadeTween();

			if (transitionLayer != null && GodotObject.IsInstanceValid(transitionLayer))
				transitionLayer.Visible = false;
			IsTransitioning = false;
		}

		/// <summary>
		/// True when the path points at a mod-provided scene (mod-relative, no
		/// <c>res://</c> prefix). Native project scenes keep the engine loader;
		/// mod scenes are built by <see cref="TscnScenes"/> and swapped in with
		/// <see cref="SceneTree.ChangeSceneToNode"/>.
		/// </summary>
		private static bool IsModScene(string scenePath)
			=> !string.IsNullOrEmpty(scenePath)
			   && !scenePath.StartsWith("res://", StringComparison.OrdinalIgnoreCase);

		private void PatchShaderMaterialsAfterLoad()
		{
			GameLogger.Log("SCENE", "PatchShaderMaterialsAfterLoad: START");
			ShaderModReplacer.PatchScene(GetTree().Root);
			GameLogger.Log("SCENE", "PatchShaderMaterialsAfterLoad: DONE");
		}

		/// <summary>
		/// Resolves a <see cref="GodotSceneType"/> to its .tscn file path via
		/// <see cref="GetScenePath"/>, publishes <see cref="SceneExitedEvent"/> for the
		/// currently loaded Godot scene and remembers its logical name for the matching
		/// <see cref="SceneEnteredEvent"/>, then delegates to <see cref="LoadScene"/>.
		/// If no file path is resolved, falls back to
		/// <see cref="SceneManager.SwitchScene"/> for scenario-based routing.
		/// </summary>
		/// <param name="sceneType">The type of Godot scene to switch to.</param>
		public void SwitchScene(GodotSceneType sceneType)
		{
			if (!string.IsNullOrEmpty(currentGodotSceneName))
			{
				ModEventBus.Publish(new SceneExitedEvent { SceneName = currentGodotSceneName });
			}

			pendingSubscriptionSceneName = sceneType.ToString().ToLowerInvariant();
			currentGodotSceneName = pendingSubscriptionSceneName;
			var path = GetScenePath(sceneType);
			if (!string.IsNullOrEmpty(path))
			{
				LoadScene(path);
				return;
			}

			GameLogger.Log("SCENE", $"SwitchScene: no scene path registered for '{sceneType}'", LogLevel.Error);
		}

		/// <summary>
		/// Switches to a scene registered by a mod through
		/// <see cref="SceneRouteRegistry"/>. Publishes the exit event for the
		/// current scene and remembers the route's logical name so the entered
		/// event carries it, then loads the mod-relative tscn. This is the route
		/// counterpart of <see cref="SwitchScene(GodotSceneType)"/>.
		/// </summary>
		public void SwitchRoute(SceneRoute route)
		{
			if (route == null || string.IsNullOrEmpty(route.ScenePath))
				return;

			if (!string.IsNullOrEmpty(currentGodotSceneName))
				ModEventBus.Publish(new SceneExitedEvent { SceneName = currentGodotSceneName });

			// Keep the logical name exactly as the mod registered it: playlist
			// scene keys and mod sound groups are matched case-sensitively.
			pendingSubscriptionSceneName = route.LogicalName;
			currentGodotSceneName = route.LogicalName;
			LoadScene(route.ScenePath);
		}

		/// <summary>
		/// Convenience method that switches to the main menu scene.
		/// Equivalent to <c>SwitchScene(GodotSceneType.MainMenu)</c>.
		/// </summary>
		public void SwitchToMenu()
		{
			SwitchScene(GodotSceneType.MainMenu);
		}

		/// <summary>
		/// Loads the base shell scene. Encapsulates the shell path so that
		/// menus, return flows and mods never hardcode
		/// <c>scenes/ui/base_scene.tscn</c>. Unlike
		/// <see cref="SwitchScene(GodotSceneType)"/> this keeps the current
		/// pending-logical-scene flow: the shell's event chain picks up
		/// <see cref="SceneManager.PendingSceneName"/> after the load and switches
		/// to the requested logical scene.
		/// </summary>
		public void LoadShell()
		{
			LoadScene(GetScenePath(GodotSceneType.BaseScene));
		}

		/// <summary>
		/// Single source of truth for scene paths by <see cref="GodotSceneType"/>.
		/// Only the main menu and the shell remain here; every gameplay scene is a
		/// mod route resolved through <see cref="SceneRouteRegistry"/>. Callers
		/// should ask for a scene type instead of hardcoding paths themselves.
		/// </summary>
		public static string GetScenePath(GodotSceneType type)
		{
			if (type == GodotSceneType.MainMenu)
				return "scenes/menu/main_menu.tscn";
			if (type == GodotSceneType.BaseScene)
				return "scenes/ui/base_scene.tscn";
			return null;
		}
	}
}
