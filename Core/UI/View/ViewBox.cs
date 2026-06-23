using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.View
{

    public partial class ViewBox : Control
    {
        [Export] private Vector2I defaultResolution = new(1920, 1024);
        [Export] private Control content;
        [Export] private float minZoom;
        [Export] private float maxZoom;

        public Control Content => content;

		public TextureRect Background { get; private set; }
		public TextureRect Foreground { get; private set; }

		private ColorRect transitionOverlay;
		private Tween transitionTween;

        public override void _Ready()
        {
            Background ??= GetNodeOrNull<TextureRect>("Background");
            Foreground ??= GetNodeOrNull<TextureRect>("Foreground");

            var timedShader = ModManager.Instance.ResolveShader("timed_sprite");
            GameLogger.Log("LIGHT", $"ViewBox._Ready: timedShader={(timedShader != null ? "OK" : "NULL")}, Bg={(Background != null ? "OK" : "NULL")}, Fg={(Foreground != null ? "OK" : "NULL")}", LogLevel.Debug);

            if (timedShader != null)
            {
                if (Background != null && Background.Material == null)
                {
                    Background.Material = new ShaderMaterial { Shader = timedShader };
                    GameLogger.Log("LIGHT", "ViewBox._Ready: created ShaderMaterial for Background", LogLevel.Debug);
                }
                if (Foreground != null && Foreground.Material == null)
                {
                    Foreground.Material = new ShaderMaterial { Shader = timedShader };
                    GameLogger.Log("LIGHT", "ViewBox._Ready: created ShaderMaterial for Foreground", LogLevel.Debug);
                }
            }

            SetZoom(0f);
            SetAlign(AlignType.CenterCenter);
        }

        public void SetBackgroundTexture(Texture2D texture)
        {
            if (Background != null)
            {
                Background.Texture = texture;
                GameLogger.Log("VIEW", $"set background '" + SceneContextNode.LastBackgroundID + "'");
            }
        }

        public void SetForegroundTexture(Texture2D texture)
        {
            if (Foreground != null)
            {
                Foreground.Texture = texture;
                GameLogger.Log("VIEW", $"set foreground");
            }
        }

        public Texture2D GetBackgroundTexture()
        {
            return Background?.Texture;
        }

		public void TransitionBackground(Texture2D newTexture, float duration = 0.5f)
		{
			if (Background == null)
			{
				if (newTexture != null)
					SetBackgroundTexture(newTexture);
				return;
			}

			if (Background.Texture == newTexture)
				return;

			KillTransitionTween();

			var overlay = GetOrCreateTransitionOverlay();
			overlay.Visible = true;
			overlay.Modulate = new Color(0, 0, 0, 0);
			overlay.MouseFilter = MouseFilterEnum.Ignore;

			var halfDuration = duration / 2f;

			transitionTween = CreateTween();
			transitionTween.TweenProperty(overlay, "modulate:a", 1f, halfDuration);
			transitionTween.TweenCallback(Callable.From(() =>
			{
				SetBackgroundTexture(newTexture);
			}));
			transitionTween.TweenProperty(overlay, "modulate:a", 0f, halfDuration);
			transitionTween.TweenCallback(Callable.From(() =>
			{
				overlay.Visible = false;
				transitionTween = null;
			}));
		}

		public void KillTransitionTween()
		{
			if (transitionTween != null)
			{
				transitionTween.Kill();
				transitionTween = null;
			}

			if (transitionOverlay != null)
			{
				transitionOverlay.Visible = false;
				transitionOverlay.Modulate = new Color(0, 0, 0, 0);
			}
		}

		private ColorRect GetOrCreateTransitionOverlay()
		{
			if (transitionOverlay == null)
			{
				transitionOverlay = new ColorRect
				{
					Name = "TransitionOverlay",
					Color = new Color(0, 0, 0, 1),
					MouseFilter = MouseFilterEnum.Ignore,
					Visible = false
				};
				transitionOverlay.AnchorRight = 1f;
				transitionOverlay.AnchorBottom = 1f;
				transitionOverlay.GrowHorizontal = GrowDirection.Both;
				transitionOverlay.GrowVertical = GrowDirection.Both;
				AddChild(transitionOverlay);
			}
			return transitionOverlay;
		}

		public void SetZoom(float value)
        {
            var zoom = minZoom + (maxZoom - minZoom) * value;
            if (content != null)
                content.Scale = Vector2.One * zoom;
        }

        public void SetAlign(AlignType align)
        {
            if (content != null)
                content.Position = ViewBoxNavigator.GetPositionByAlign(align, content.Size);
        }

#if TOOLS && DEBUG
        public override void _ValidateProperty(Godot.Collections.Dictionary property)
        {
            base._ValidateProperty(property);
            if (content != null)
                content.Size = defaultResolution;
        }
#endif
    }

}
