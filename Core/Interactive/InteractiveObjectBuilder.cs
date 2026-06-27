using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.View;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Interactive
{
	/// <summary>
	/// Fluent builder for creating interactive objects programmatically from
	/// wrapper-template C# code. The texture is always full-screen with alpha;
	/// the collider defines the clickable region in normalised viewport coordinates.
	/// </summary>
	public class InteractiveObjectBuilder
	{
		private readonly string _id;
		private readonly InteractiveManager _manager;

		private string _layer = "foreground";
		private string _texture;
		private Texture2D _textureDirect;
		private int _zIndex;
		private bool _enabled = true;
		private bool _visible = true;
		private string _cursor;

		private HitAreaType _hitType = HitAreaType.Rect;
		private float _hitX, _hitY, _hitW = 0.1f, _hitH = 0.1f;
		private float _hitRadius = 0.05f;
		private Vector2[] _hitVertices;

		private Color _highlightColor = new(1f, 1f, 0f, 0.3f);
		private float _highlightScale = 1.02f;
		private float _highlightDuration = 0.15f;

		private Action<string> _onClick;
		private Action<string> _onHoverEnter;
		private Action<string> _onHoverLeave;
		private string _onHoverEnterDsl;
		private string _onHoverLeaveDsl;

		internal InteractiveObjectBuilder(string id, InteractiveManager manager)
		{
			_id = id;
			_manager = manager;
		}

		/// <summary>Sets the visual layer ("background", "foreground" or "ui"). Default is "foreground".</summary>
		public InteractiveObjectBuilder SetLayer(string layer) { _layer = layer; return this; }

		/// <summary>Sets the full-screen texture key (resolved via ModManager.ResolveTexture).</summary>
		public InteractiveObjectBuilder SetTexture(string textureKey) { _texture = textureKey; return this; }

		/// <summary>Sets the full-screen texture directly.</summary>
		public InteractiveObjectBuilder SetTexture(Texture2D texture) { _textureDirect = texture; return this; }

		/// <summary>Sets the Z-index for ordering within the layer.</summary>
		public InteractiveObjectBuilder SetZIndex(int z) { _zIndex = z; return this; }

		/// <summary>Sets whether the object responds to input. Default true.</summary>
		public InteractiveObjectBuilder SetEnabled(bool enabled) { _enabled = enabled; return this; }

		/// <summary>Sets whether the object is visible. Default true.</summary>
		public InteractiveObjectBuilder SetVisible(bool visible) { _visible = visible; return this; }

		/// <summary>Sets the mouse cursor shape (Godot CursorShape enum name, e.g. "PointingHand").</summary>
		public InteractiveObjectBuilder SetCursor(string cursor) { _cursor = cursor; return this; }

		/// <summary>Sets a rectangular collider at normalised viewport coordinates (0..1).</summary>
		public InteractiveObjectBuilder SetHitRect(float x, float y, float width, float height)
		{
			_hitType = HitAreaType.Rect;
			_hitX = x; _hitY = y; _hitW = width; _hitH = height;
			return this;
		}

		/// <summary>Sets a circular collider centred at normalised coordinates with normalised radius.</summary>
		public InteractiveObjectBuilder SetHitCircle(float x, float y, float radius)
		{
			_hitType = HitAreaType.Circle;
			_hitX = x; _hitY = y; _hitRadius = radius;
			return this;
		}

		/// <summary>Sets a polygon collider from an array of normalised vertices (0..1).</summary>
		public InteractiveObjectBuilder SetHitPolygon(Vector2[] vertices)
		{
			_hitType = HitAreaType.Polygon;
			_hitVertices = vertices;
			return this;
		}

		/// <summary>Configures the highlight effect.</summary>
		public InteractiveObjectBuilder WithHighlight(Color color, float scale = 1.02f, float duration = 0.15f)
		{
			_highlightColor = color;
			_highlightScale = scale;
			_highlightDuration = duration;
			return this;
		}

		/// <summary>Sets the click callback. Receives the object's ID.</summary>
		public InteractiveObjectBuilder OnClick(Action<string> callback) { _onClick = callback; return this; }

		/// <summary>Sets the hover-enter callback.</summary>
		public InteractiveObjectBuilder OnHoverEnter(Action<string> callback) { _onHoverEnter = callback; return this; }

		/// <summary>Sets the hover-leave callback.</summary>
		public InteractiveObjectBuilder OnHoverLeave(Action<string> callback) { _onHoverLeave = callback; return this; }

		/// <summary>Sets inline DSL command executed on hover enter.</summary>
		public InteractiveObjectBuilder SetHoverEnterDsl(string dsl) { _onHoverEnterDsl = dsl; return this; }

		/// <summary>Sets inline DSL command executed on hover leave.</summary>
		public InteractiveObjectBuilder SetHoverLeaveDsl(string dsl) { _onHoverLeaveDsl = dsl; return this; }

		/// <summary>
		/// Finalises configuration, creates the Godot node, registers it with
		/// <c>InteractiveManager</c>, and returns the built <c>InteractiveObject</c>.
		/// </summary>
		public InteractiveObject Build()
		{
			var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
			if (viewBox == null)
			{
				GameLogger.Log("INTERACTIVE", "InteractiveObjectBuilder.Build: ViewBox not found", LogLevel.Error);
				return null;
			}

			var layerContainer = viewBox.GetInteractiveLayer(_layer);
			if (layerContainer == null)
			{
				GameLogger.Log("INTERACTIVE", $"InteractiveObjectBuilder.Build: layer '{_layer}' not found", LogLevel.Error);
				return null;
			}

			var contentSize = viewBox.LogicalSize != Vector2I.Zero
				? new Vector2(viewBox.LogicalSize.X, viewBox.LogicalSize.Y)
				: viewBox.Content?.Size ?? new Vector2(1920f, 1024f);

			var obj = new InteractiveObject
			{
				Name = "Interactive_" + _id,
				ID = _id
			};

			layerContainer.AddChild(obj);

			var def = BuildDefinition();

			obj.Configure(def, contentSize);

			if (_textureDirect != null)
			{
				var visual = obj.GetNodeOrNull<TextureRect>("Visual");
				if (visual != null)
					visual.Texture = _textureDirect;
			}

			if (_onClick != null)
				obj.OnClickCallback = _onClick;

			if (_onHoverEnter != null)
				obj.OnHoverEnterCallback = _onHoverEnter;

			if (_onHoverLeave != null)
				obj.OnHoverLeaveCallback = _onHoverLeave;

			_manager?.RegisterBuiltObject(obj);

			GameLogger.Log("INTERACTIVE", $"InteractiveObjectBuilder.Build: '{_id}' created");
			return obj;
		}

		private InteractiveDefinition BuildDefinition()
		{
			var hitArea = new HitAreaDefinition
			{
				Type = _hitType.ToString().ToLowerInvariant()
			};

			switch (_hitType)
			{
				case HitAreaType.Rect:
					hitArea.X = _hitX;
					hitArea.Y = _hitY;
					hitArea.Width = _hitW;
					hitArea.Height = _hitH;
					break;
				case HitAreaType.Circle:
					hitArea.X = _hitX;
					hitArea.Y = _hitY;
					hitArea.Radius = _hitRadius;
					break;
				case HitAreaType.Polygon:
					hitArea.Vertices = _hitVertices;
					break;
			}

			return new InteractiveDefinition
			{
				ID = _id,
				Texture = _texture,
				Layer = _layer,
				ZIndex = _zIndex,
				Enabled = _enabled,
				Visible = _visible,
				Cursor = _cursor,
				HitArea = hitArea,
				Highlight = new HighlightDefinition
				{
					ColorHex = $"#{_highlightColor.ToHtml(false)}",
					Scale = _highlightScale,
					Duration = _highlightDuration
				}
			};
		}
	}
}
