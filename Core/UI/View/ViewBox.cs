using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.View
{
	/// <summary>
	/// Core viewport container: manages background/foreground TextureRects with
	/// a shared shader material (timed_sprite from mods) for time-of-day lighting.
	/// Supports smooth background transitions via a black ColorRect overlay that
	/// fades in, swaps the texture mid-transition, then fades out — a classic
	/// crossfade approach that doesn't require a second TextureRect. The zoom
	/// system scales the content Control between minZoom and maxZoom based on
	/// a 0–1 input value. Alignment positions content within the viewport using
	/// ViewBoxNavigator's 9-point grid. The #if TOOLS block forces content size
	/// to defaultResolution for editor preview.
	///
	/// Interactive layers are Control containers placed between Background and
	/// Foreground (and above Foreground for UI) that host interactive scene objects.
	/// </summary>
	public partial class ViewBox : Control
	{
		[Export] private Vector2I defaultResolution = new(1920, 1024);
		[Export] private Control content;
		[Export] private float minZoom;
		[Export] private float maxZoom;

		public Control Content => content;

		public TextureRect Background { get; private set; }
		public TextureRect Foreground { get; private set; }

		/// <summary>Container for interactive objects rendered behind the foreground.</summary>
		public Control InteractiveLayerBackground { get; private set; }

		/// <summary>Container for interactive objects rendered above the foreground.</summary>
		public Control InteractiveLayerForeground { get; private set; }

		/// <summary>Container for interactive objects rendered above all scene visuals (UI layer).</summary>
		public Control InteractiveLayerUI { get; private set; }

		private ColorRect transitionOverlay;
		private Tween transitionTween;

		public override void _Ready()
		{
			Background ??= GetNodeOrNull<TextureRect>("Background");
			Foreground ??= GetNodeOrNull<TextureRect>("Foreground");

			if (content != null)
			{
				InteractiveLayerBackground = CreateLayer("InteractiveLayer_Background", 1);
				InteractiveLayerForeground = CreateLayer("InteractiveLayer_Foreground", 3);
				InteractiveLayerUI = CreateLayer("InteractiveLayer_UI", 5);
			}

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

		/// <summary>
		/// Returns the interactive layer container for the given logical layer name.
		/// </summary>
		/// <param name="layerName">"background", "foreground" or "ui".</param>
		/// <returns>The matching Control container, or <c>InteractiveLayerForeground</c> as fallback.</returns>
		public Control GetInteractiveLayer(string layerName)
		{
			return layerName?.ToLowerInvariant() switch
			{
				"background" => InteractiveLayerBackground,
				"foreground" => InteractiveLayerForeground,
				"ui" => InteractiveLayerUI,
				_ => InteractiveLayerForeground
			};
		}

		private Control CreateLayer(string name, int zIndex)
		{
			var layer = new Control
			{
				Name = name,
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = zIndex
			};
			layer.SetAnchorsPreset(LayoutPreset.FullRect);
			content.AddChild(layer);
			return layer;
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
