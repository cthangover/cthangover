using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Interactive
{
	/// <summary>
	/// Runtime node for an interactive scene object. Inherits <c>Control</c> for
	/// UI-tree compatibility and implements <c>IEventObject</c> for automatic
	/// lifecycle cleanup by <c>SceneContextNode</c> on scene changes.
	///
	/// Hit detection uses <c>_HasPoint</c> overrides supporting rect, circle
	/// and polygon shapes. A child <c>TextureRect</c> provides the visual
	/// representation with an optional shader-based highlight on hover.
	/// Position and size are computed from normalised definition coordinates
	/// relative to <c>ViewBox.Content</c> logical size.
	///
	/// Callbacks for hover/click actions are injected by <c>InteractiveManager</c>
	/// to bridge with the scenario/dialog system.
	/// </summary>
	public partial class InteractiveObject : Control, IEventObject
	{
		private static readonly Color _transparent = new(0f, 0f, 0f, 0f);

		/// <summary>Unique instance identifier, used for registry and <c>IEventObject</c> tracking.</summary>
		public string ID { get; set; }

		/// <summary>Reference back to the <c>InteractiveDefinition.ID</c> that spawned this object.</summary>
		public string DefinitionId { get; private set; }

		/// <summary>Whether the object responds to mouse input.</summary>
		public bool IsEnabled { get; set; } = true;

		/// <summary>True while the pointer is within the hit area.</summary>
		public bool IsHovered { get; private set; }

		/// <summary>Called when the pointer enters the hit area. DSL string from definition passed as argument.</summary>
		internal Action<string> OnHoverEnterCallback { get; set; }

		/// <summary>Called when the pointer leaves the hit area. DSL string from definition passed as argument.</summary>
		internal Action<string> OnHoverLeaveCallback { get; set; }

		/// <summary>Called on left-click. The definition ID is passed as argument.</summary>
		internal Action<string> OnClickCallback { get; set; }

		private TextureRect _visual;
		private ShaderMaterial _highlightMaterial;
		private Tween _highlightTween;

		private HitAreaType _hitType = HitAreaType.Rect;
		private float _hitRadius;
		private Vector2[] _hitPolygon;
		private Vector2 _pixelSize;

		private Color _highlightColor = _transparent;
		private float _highlightScale = 1f;
		private float _highlightDuration = 0.15f;

		private string _onHoverEnterDsl;
		private string _onHoverLeaveDsl;

		private ColorRect _debugRect;
		private Label _debugLabel;
		private bool _visualReady;

		/// <summary>
		/// Ensures visual children exist and signals are wired. Idempotent —
		/// safe to call from both <c>_Ready</c> and <c>Configure</c>.
		/// </summary>
		public override void _Ready()
		{
			MouseFilter = MouseFilterEnum.Stop;
			ClipContents = true;

			EnsureVisualReady();

			MouseEntered += OnMouseEntered;
			MouseExited += OnMouseExited;
		}

		/// <summary>
		/// Applies a definition to this instance, computing pixel coordinates
		/// from normalised values and loading textures, hit-area shapes,
		/// highlight settings and action bindings.
		/// </summary>
		/// <param name="def">The definition to apply.</param>
		/// <param name="contentSize">Logical pixel size of the parent container (typically <c>ViewBox.Content.Size</c>).</param>
		public void Configure(InteractiveDefinition def, Vector2 contentSize)
		{
			if (def == null)
				return;

			DefinitionId = def.ID;

			EnsureVisualReady();

			_pixelSize = def.Size * contentSize;
			var pixelPos = def.Position * contentSize;
			var anchorOffset = def.Anchor * _pixelSize;
			Position = pixelPos - anchorOffset;
			Size = _pixelSize;
			ZIndex = def.ZIndex;
			Visible = def.Visible;
			IsEnabled = def.Enabled;

			_visual.Size = _pixelSize * def.Scale;
			_visual.Position = (_pixelSize - _visual.Size) / 2f;

			if (!string.IsNullOrEmpty(def.Texture))
			{
				var tex = ModManager.Instance.ResolveTexture(def.Texture);
				if (tex != null)
					_visual.Texture = tex;
			}

			ApplyHitArea(def.HitArea);

			if (def.Highlight != null)
			{
				_highlightColor = def.Highlight.Color;
				_highlightScale = def.Highlight.Scale;
				_highlightDuration = def.Highlight.Duration;
			}

			_onHoverEnterDsl = def.Actions?.OnHoverEnter;
			_onHoverLeaveDsl = def.Actions?.OnHoverLeave;

			SetCursor(def.Cursor);
		}

		/// <summary>
		/// Overrides Godot's hit-test to support non-rectangular shapes.
		/// Point is in local coordinates.
		/// </summary>
		public override bool _HasPoint(Vector2 point)
		{
			if (!IsEnabled || !Visible)
				return false;

			var localPoint = point - Size / 2f;

			switch (_hitType)
			{
				case HitAreaType.Circle:
					return localPoint.Length() <= _hitRadius;
				case HitAreaType.Polygon:
					return _hitPolygon != null && Geometry2D.IsPointInPolygon(localPoint, _hitPolygon);
				default:
					return true;
			}
		}

		/// <summary>Cleans up the node and removes it from the scene tree. Called by <c>SceneContextNode</c> on scene change.</summary>
		public void Destruct()
		{
			SceneContextNode.Instance?.RemoveEventObject(ID);
			QueueFree();
		}

		/// <summary>Shows or hides the debug overlay for this object.</summary>
		/// <param name="show">If true, creates and reveals the debug bounds rect.</param>
		public void UpdateDebugVisual(bool show)
		{
			if (show)
			{
				if (_debugRect == null)
				{
					_debugRect = new ColorRect
					{
						Name = "DebugRect",
						MouseFilter = MouseFilterEnum.Ignore,
						ZIndex = 100
					};
					_debugRect.Size = Size;
					_debugRect.Position = Vector2.Zero;
					AddChild(_debugRect);
				}

				if (_debugLabel == null)
				{
					_debugLabel = new Label
					{
						Name = "DebugLabel",
						MouseFilter = MouseFilterEnum.Ignore,
						ZIndex = 101,
						Position = new Vector2(2f, 2f)
					};
					AddChild(_debugLabel);
				}

				_debugRect.Visible = true;
				_debugRect.Color = IsHovered
					? new Color(1f, 1f, 0f, 0.35f)
					: new Color(0f, 1f, 0f, 0.2f);

				_debugLabel.Visible = true;
				_debugLabel.Text = DefinitionId ?? ID;
			}
			else
			{
				if (_debugRect != null)
					_debugRect.Visible = false;
				if (_debugLabel != null)
					_debugLabel.Visible = false;
			}
		}

		private void SetupHighlightMaterial()
		{
			var shader = ModManager.Instance.ResolveShader("interactive_highlight");
			if (shader == null)
			{
				GameLogger.Log("INTERACTIVE", "highlight shader 'interactive_highlight' not found", LogLevel.Warning);
				return;
			}

			_highlightMaterial = new ShaderMaterial { Shader = shader };
			_highlightMaterial.SetShaderParameter("modulate_color", _transparent);
			_visual.Material = _highlightMaterial;
		}

		private void EnsureVisualReady()
		{
			if (_visualReady)
				return;

			_visualReady = true;

			_visual = new TextureRect
			{
				Name = "Visual",
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				MouseFilter = MouseFilterEnum.Ignore
			};
			AddChild(_visual);

			SetupHighlightMaterial();
		}

		private void ApplyHitArea(HitAreaDefinition hitDef)
		{
			_hitType = HitAreaType.Rect;
			if (hitDef == null)
				return;

			switch (hitDef.Type?.ToLowerInvariant())
			{
				case "circle":
					_hitType = HitAreaType.Circle;
					_hitRadius = hitDef.Radius * Mathf.Min(_pixelSize.X, _pixelSize.Y) * 0.5f;
					break;
				case "polygon":
					_hitType = HitAreaType.Polygon;
					if (hitDef.Vertices != null && hitDef.Vertices.Length >= 3)
					{
						_hitPolygon = new Vector2[hitDef.Vertices.Length];
						for (var i = 0; i < hitDef.Vertices.Length; i++)
							_hitPolygon[i] = hitDef.Vertices[i] * _pixelSize - _pixelSize / 2f;
					}
					break;
			}
		}

		private void SetCursor(string cursorName)
		{
			if (string.IsNullOrEmpty(cursorName))
				return;

			if (Enum.TryParse<CursorShape>(cursorName, true, out var shape))
				MouseDefaultCursorShape = shape;
		}

		private void OnMouseEntered()
		{
			if (!IsEnabled)
				return;

			IsHovered = true;
			ApplyHighlight(true);
			UpdateDebugVisual(InteractiveManager.Instance?.ShowDebugBounds ?? false);
			OnHoverEnterCallback?.Invoke(_onHoverEnterDsl);
		}

		private void OnMouseExited()
		{
			if (!IsEnabled)
				return;

			IsHovered = false;
			ApplyHighlight(false);
			UpdateDebugVisual(InteractiveManager.Instance?.ShowDebugBounds ?? false);
			OnHoverLeaveCallback?.Invoke(_onHoverLeaveDsl);
		}

		private void ApplyHighlight(bool active)
		{
			if (_highlightMaterial == null)
				return;

			_highlightTween?.Kill();

			var targetScale = active ? _highlightScale : 1f;

			_highlightTween = CreateTween();
			_highlightTween.SetParallel(true);

			_highlightTween.TweenProperty(_visual, "scale", Vector2.One * targetScale, _highlightDuration);

			if (_highlightColor.A > 0f)
			{
				_highlightTween.TweenMethod(
					Callable.From<float>(f =>
					{
						var c = new Color(_highlightColor.R, _highlightColor.G, _highlightColor.B, _highlightColor.A * f);
						_highlightMaterial.SetShaderParameter("modulate_color", c);
					}),
					active ? 0f : 1f,
					active ? 1f : 0f,
					_highlightDuration
				);
			}
		}

		/// <summary>
		/// Handles left-click input on the hit area. Fires the click callback
		/// defined by <c>InteractiveManager</c> and consumes the event.
		/// </summary>
		public override void _GuiInput(InputEvent @event)
		{
			if (!IsEnabled)
				return;

			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				GameLogger.Log("INTERACTIVE", $"clicked: '{DefinitionId}'");
				OnClickCallback?.Invoke(DefinitionId);
				AcceptEvent();
			}
		}
	}
}
