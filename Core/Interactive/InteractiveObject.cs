using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Interactive
{
	/// <summary>
	/// Runtime node for an interactive scene object. The Control fills its parent
	/// (the interactive layer container) and displays a full-screen texture with
	/// alpha. The collider (rect, circle, or polygon) defines the clickable region
	/// in normalised viewport coordinates, converted to pixels at runtime.
	///
	/// Hit detection uses <c>_HasPoint</c> overrides verified against global
	/// mouse position and the pixel-space collider geometry.
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

		/// <summary>True while the pointer is within the collider.</summary>
		public bool IsHovered { get; private set; }

		internal Action<string> OnHoverEnterCallback { get; set; }
		internal Action<string> OnHoverLeaveCallback { get; set; }
		internal Action<string> OnClickCallback { get; set; }

		private TextureRect _visual;
		private ShaderMaterial _highlightMaterial;
		private Tween _highlightTween;

		private HitAreaType _hitType = HitAreaType.Rect;
		private float _hitRadiusPx;
		private Vector2[] _hitPolygonPx;
		private Rect2 _hitRectPx;

		private Vector2 _contentSize;

		private Color _highlightColor = _transparent;
		private float _highlightScale = 1f;
		private float _highlightDuration = 0.15f;

		private string _onHoverEnterDsl;
		private string _onHoverLeaveDsl;

		private ColorRect _debugRect;
		private Label _debugLabel;
		private bool _visualReady;

		public override void _Ready()
		{
			MouseFilter = MouseFilterEnum.Stop;
			ClipContents = true;

			EnsureVisualReady();

			MouseEntered += OnMouseEntered;
			MouseExited += OnMouseExited;

			GameLogger.Log("INTERACTIVE", $"_Ready: id='{ID}' size=({Size.X:F0}x{Size.Y:F0}) visible={Visible} enabled={IsEnabled}");
		}

		/// <summary>
		/// Applies a definition to this instance. The Control is made full-screen
		/// within its parent, the texture fills the entire area, and the collider
		/// geometry is computed from normalised coordinates multiplied by the
		/// logical viewport size.
		/// </summary>
		public void Configure(InteractiveDefinition def, Vector2 contentSize)
		{
			if (def == null)
				return;

			DefinitionId = def.ID;
			_contentSize = contentSize;

			EnsureVisualReady();

			AnchorLeft = 0f;
			AnchorTop = 0f;
			AnchorRight = 1f;
			AnchorBottom = 1f;

			ZIndex = def.ZIndex;
			Visible = def.Visible;
			IsEnabled = def.Enabled;

			if (!string.IsNullOrEmpty(def.Texture))
			{
				var tex = ModManager.Instance.ResolveTexture(def.Texture);
				if (tex != null)
					_visual.Texture = tex;
			}

			ApplyHitArea(def.HitArea, contentSize);

			if (def.Highlight != null)
			{
				_highlightColor = def.Highlight.Color;
				_highlightScale = def.Highlight.Scale;
				_highlightDuration = def.Highlight.Duration;
			}

			_onHoverEnterDsl = def.Actions?.OnHoverEnter;
			_onHoverLeaveDsl = def.Actions?.OnHoverLeave;

			SetCursor(def.Cursor);

			GameLogger.Log("INTERACTIVE", $"Configure: id='{def.ID}' fullScreen contentSize=({contentSize.X:F0},{contentSize.Y:F0}) hitType={_hitType}");
		}

		/// <summary>
		/// Checks whether the global mouse position falls within the collider
		/// geometry in pixel space. Bypasses Godot's local coordinate issues.
		/// </summary>
		public override bool _HasPoint(Vector2 point)
		{
			if (!IsEnabled || !Visible)
				return false;

			var globalMouse = GetGlobalMousePosition();
			var globalPos = GlobalPosition;

			switch (_hitType)
			{
				case HitAreaType.Rect:
				{
					var rect = new Rect2(globalPos + _hitRectPx.Position, _hitRectPx.Size);
					return rect.HasPoint(globalMouse);
				}
				case HitAreaType.Circle:
				{
					var center = globalPos + new Vector2(_hitRectPx.Position.X + _hitRectPx.Size.X / 2f, _hitRectPx.Position.Y + _hitRectPx.Size.Y / 2f);
					return (globalMouse - center).Length() <= _hitRadiusPx;
				}
				case HitAreaType.Polygon:
				{
					if (_hitPolygonPx == null || _hitPolygonPx.Length < 3)
						return false;
					var local = globalMouse - globalPos;
					return Geometry2D.IsPointInPolygon(local, _hitPolygonPx);
				}
			}

			return false;
		}

		public void Destruct()
		{
			SceneContextNode.Instance?.RemoveEventObject(ID);
			QueueFree();
		}

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

				_debugRect.Position = _hitRectPx.Position;
				_debugRect.Size = _hitRectPx.Size;
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
				MouseFilter = MouseFilterEnum.Ignore,
				AnchorRight = 1f,
				AnchorBottom = 1f
			};
			AddChild(_visual);

			SetupHighlightMaterial();
		}

		private void ApplyHitArea(HitAreaDefinition hitDef, Vector2 contentSize)
		{
			_hitType = HitAreaType.Rect;
			var vpW = contentSize.X;
			var vpH = contentSize.Y;

			if (hitDef == null)
			{
				_hitRectPx = new Rect2(0, 0, vpW, vpH);
				return;
			}

			switch (hitDef.Type?.ToLowerInvariant())
			{
				case "circle":
					_hitType = HitAreaType.Circle;
					_hitRadiusPx = hitDef.Radius * Mathf.Min(vpW, vpH);
					var cx = hitDef.X * vpW;
					var cy = hitDef.Y * vpH;
					_hitRectPx = new Rect2(cx - _hitRadiusPx, cy - _hitRadiusPx, _hitRadiusPx * 2f, _hitRadiusPx * 2f);
					break;

				case "polygon":
					_hitType = HitAreaType.Polygon;
					if (hitDef.Vertices != null && hitDef.Vertices.Length >= 3)
					{
						_hitPolygonPx = new Vector2[hitDef.Vertices.Length];
						float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
						for (var i = 0; i < hitDef.Vertices.Length; i++)
						{
							var v = new Vector2(hitDef.Vertices[i].X * vpW, hitDef.Vertices[i].Y * vpH);
							_hitPolygonPx[i] = v;
							minX = Mathf.Min(minX, v.X);
							minY = Mathf.Min(minY, v.Y);
							maxX = Mathf.Max(maxX, v.X);
							maxY = Mathf.Max(maxY, v.Y);
						}
						_hitRectPx = new Rect2(minX, minY, maxX - minX, maxY - minY);
					}
					break;

				default:
					_hitType = HitAreaType.Rect;
					var rx = hitDef.X * vpW;
					var ry = hitDef.Y * vpH;
					var rw = hitDef.Width * vpW;
					var rh = hitDef.Height * vpH;
					_hitRectPx = new Rect2(rx, ry, rw, rh);
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
			GameLogger.Log("INTERACTIVE", $"OnMouseEntered: id='{ID}' enabled={IsEnabled} hitType={_hitType}");

			if (!IsEnabled)
				return;

			IsHovered = true;
			ApplyHighlight(true);
			UpdateDebugVisual(InteractiveManager.Instance?.ShowDebugBounds ?? false);
			OnHoverEnterCallback?.Invoke(_onHoverEnterDsl);
		}

		private void OnMouseExited()
		{
			GameLogger.Log("INTERACTIVE", $"OnMouseExited: id='{ID}'");

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

			_highlightTween.TweenProperty(this, "scale", Vector2.One * targetScale, _highlightDuration);

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

		public override void _GuiInput(InputEvent @event)
		{
			GameLogger.Log("INTERACTIVE", $"_GuiInput: id='{ID}' event={@event.GetType().Name} enabled={IsEnabled}");

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
