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

		/// <summary>
		/// The content node that holds all scene visuals. Scaling and alignment are applied
		/// directly to this node.
		/// </summary>
		public Control Content => content;

		/// <summary>Logical resolution of the content area (default 1920x1024). Used for normalised coordinate calculations.</summary>
		public Vector2I LogicalSize => defaultResolution;

		/// <summary>
		/// The background <see cref="TextureRect"/> rendered behind all interactive layers.
		/// Receives a <c>timed_sprite</c> shader material for time-of-day lighting when
		/// no material is already assigned.
		/// </summary>
		public TextureRect Background { get; private set; }

		/// <summary>
		/// The foreground <see cref="TextureRect"/> rendered above interactive layers.
		/// Receives the same <c>timed_sprite</c> shader as the background.
		/// </summary>
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

			var timedShader = ModManager.Instance.ResolveShader("timed_sprite");

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
		/// Creates the layer lazily as a direct child of ViewBox if it doesn't exist yet.
		/// </summary>
		/// <param name="layerName">"background", "foreground" or "ui".</param>
		/// <returns>The matching Control container.</returns>
		public Control GetInteractiveLayer(string layerName)
		{
			var result = layerName?.ToLowerInvariant() switch
			{
				"background" => InteractiveLayerBackground ??= CreateLayerDirect("InteractiveLayer_Background", 1),
				"foreground" => InteractiveLayerForeground ??= CreateLayerDirect("InteractiveLayer_Foreground", 3),
				"ui" => InteractiveLayerUI ??= CreateLayerDirect("InteractiveLayer_UI", 5),
				_ => InteractiveLayerForeground ??= CreateLayerDirect("InteractiveLayer_Foreground", 3)
			};

			return result;
		}

		private Control CreateLayerDirect(string name, int zIndex)
		{
			var layer = new Control
			{
				Name = name,
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = zIndex,
				Size = new Vector2(defaultResolution.X, defaultResolution.Y)
			};
			AddChild(layer);
			return layer;
		}

		/// <summary>
		/// Assigns a texture to the background <see cref="TextureRect"/>. This is the
		/// primary mechanism for scenes to set the backdrop visual. Logs the new background
		/// ID from <see cref="SceneContextNode.LastBackgroundID"/> for debugging.
		/// </summary>
		public void SetBackgroundTexture(Texture2D texture)
		{
			if (Background != null)
			{
				Background.Texture = texture;
				GameLogger.Log("VIEW", $"set background '" + SceneContextNode.LastBackgroundID + "'");
			}
		}

		/// <summary>
		/// Assigns a texture to the foreground <see cref="TextureRect"/>. Typically used
		/// for scene overlays rendered above interactive objects but below UI.
		/// </summary>
		public void SetForegroundTexture(Texture2D texture)
		{
			if (Foreground != null)
			{
				Foreground.Texture = texture;
				GameLogger.Log("VIEW", $"set foreground");
			}
		}

		/// <summary>
		/// Returns the currently assigned background texture, or <c>null</c> if none is set
		/// or the <see cref="Background"/> node is missing.
		/// </summary>
		public Texture2D GetBackgroundTexture()
		{
			return Background?.Texture;
		}

		/// <summary>
		/// Smoothly transitions the background to a new texture using a black overlay crossfade:
		/// fades a <see cref="ColorRect"/> to full opacity over the first half of <paramref name="duration"/>,
		/// swaps the background texture mid-transition, then fades back out over the second half.
		/// If the target texture is already set or the background node is missing, no-op.
		/// <see cref="KillTransitionTween"/> cancels any in-progress transition first.
		/// </summary>
		/// <param name="newTexture">The replacement background texture.</param>
		/// <param name="duration">Total crossfade duration in seconds (default 0.5).</param>
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

		/// <summary>
		/// Aborts any in-progress background transition by killing the tween and resetting
		/// the overlay to invisible. Safe to call when no transition is active.
		/// </summary>
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

		/// <summary>
		/// Applies a zoom level to the content node. The <paramref name="value"/> is a 0–1 lerp
		/// factor between <c>minZoom</c> and <c>maxZoom</c>. Scale is uniform (X and Y are equal).
		/// </summary>
		/// <param name="value">Normalized zoom factor: 0 = minZoom, 1 = maxZoom.</param>
		public void SetZoom(float value)
		{
			var zoom = minZoom + (maxZoom - minZoom) * value;
			if (content != null)
				content.Scale = Vector2.One * zoom;
		}

		/// <summary>
		/// Positions the content node within the viewport using the 9-point alignment grid
		/// from <see cref="ViewBoxNavigator.GetPositionByAlign"/>.
		/// </summary>
		/// <param name="align">Which corner/edge/center to align to.</param>
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
