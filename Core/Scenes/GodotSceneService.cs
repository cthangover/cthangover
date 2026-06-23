using Cthangover.Core.Audio;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{
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

		public static bool IsTransitioning { get; private set; }

		public override void _Ready()
		{
			base._Ready();
			ProcessMode = ProcessModeEnum.Always;
			sceneManager = GetNode<SceneManager>("/root/SceneManager");
		}

		public void LoadScene(string nextScene)
		{
			GameLogger.Log("SCENE", $"load scene '{nextScene}'...");

			foreach (var musicPlayer in GetTree().GetNodesInGroup("music_player"))
			{
				if (musicPlayer is MusicPlayerBehaviour cast)
				{
					GameLogger.Log("AUDIO", $"GodotSceneService notifying MusicPlayer about scene '{nextScene}'");
					var fileName = System.IO.Path.GetFileNameWithoutExtension(nextScene);
					if (System.Enum.TryParse<GodotSceneType>(fileName, out var sceneType))
						cast.UpdateMusicType(sceneType);
				}
			}

			BeginSceneTransition(nextScene);
		}

		private void BeginSceneTransition(string nextScene)
		{
			pendingScenePath = nextScene;

			if (!IsBattlePath(pendingScenePath))
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

		private static bool IsBattlePath(string scenePath)
		{
			return scenePath != null && scenePath.Contains("Battle");
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

			var shader = ModManager.Instance.ResolveShader("scene_transition");
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

			tree.ChangeSceneToFile(pendingScenePath);

			CallDeferred(nameof(PatchShaderMaterialsAfterLoad));
			CallDeferred(nameof(RunSubscriptionAfterLoad));

			if (IsBattlePath(pendingScenePath))
				CallDeferred(nameof(StartFadeInAfterSwitch));
		}

		private void RunSubscriptionAfterLoad()
		{
			if (string.IsNullOrEmpty(pendingSubscriptionSceneName))
				return;

			var tree = GetTree();
			if (tree == null)
				return;

			SceneSubscriptionRegistry.RunSubscriptions(pendingSubscriptionSceneName, tree.CurrentScene);
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

		private void PatchShaderMaterialsAfterLoad()
		{
			ShaderModReplacer.PatchScene(GetTree().Root);
		}

        public void SwitchScene(GodotSceneType sceneType)
        {
            if (!string.IsNullOrEmpty(currentGodotSceneName))
            {
                var tree = GetTree();
                if (tree != null)
                    SceneSubscriptionRegistry.RunExitSubscriptions(currentGodotSceneName, tree.CurrentScene);
            }

            pendingSubscriptionSceneName = sceneType.ToString().ToLowerInvariant();
            currentGodotSceneName = pendingSubscriptionSceneName;
            var path = GetScenePath(sceneType);
            if (!string.IsNullOrEmpty(path))
            {
                LoadScene(path);
                return;
            }

            sceneManager?.SwitchScene(sceneType.ToString());
        }

        public void SwitchToMenu()
        {
            SwitchScene(GodotSceneType.MainMenu);
        }

        public void SwitchToBattle()
        {
            SwitchScene(GodotSceneType.Battle);
        }

        private static string GetScenePath(GodotSceneType type)
        {
            if (type == GodotSceneType.Battle)
                return "res://Scenes/Battle.tscn";
            if (type == GodotSceneType.MainMenu)
                return "res://Scenes/MainMenu.tscn";
            if (type == GodotSceneType.BaseScene)
                return "res://Scenes/BaseScene.tscn";
            return null;
        }
    }
}
