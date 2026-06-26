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
	/// wrapper-template C# code. Provides a chainable API for setting visual
	/// properties, hit-area geometry, highlight effects, and callbacks.
	///
	/// Call <c>Build()</c> to finalise and register the object with
	/// <c>InteractiveManager</c>.
	/// </summary>
	public class InteractiveObjectBuilder
	{
		private readonly string _id;
		private readonly InteractiveManager _manager;

		private string _layer = "foreground";
		private string _texture;
		private Texture2D _textureDirect;
		private Vector2 _position;
		private Vector2 _size = new(0.1f, 0.1f);
		private Vector2 _anchor = new(0.5f, 0.5f);
		private Vector2 _scale = Vector2.One;
		private int _zIndex;
		private bool _enabled = true;
		private bool _visible = true;
		private string _cursor;

		private HitAreaType _hitType = HitAreaType.Rect;
		private float _hitRadius = 0.5f;
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

		/// <summary>Sets the texture by key (resolved via ModManager.ResolveTexture).</summary>
		public InteractiveObjectBuilder SetTexture(string textureKey) { _texture = textureKey; return this; }

		/// <summary>Sets the texture directly from a Godot Texture2D reference.</summary>
		public InteractiveObjectBuilder SetTexture(Texture2D texture) { _textureDirect = texture; return this; }

		/// <summary>Sets normalised position (0..1 relative to ViewBox.Content).</summary>
		public InteractiveObjectBuilder SetPosition(float x, float y) { _position = new Vector2(x, y); return this; }

		/// <summary>Sets normalised size (0..1 relative to ViewBox.Content).</summary>
		public InteractiveObjectBuilder SetSize(float width, float height) { _size = new Vector2(width, height); return this; }

		/// <summary>Sets the anchor point within the object (0..1). (0.5, 0.5) = centre.</summary>
		public InteractiveObjectBuilder SetAnchor(float x, float y) { _anchor = new Vector2(x, y); return this; }

		/// <summary>Sets the visual scale multiplier. Default is (1,1).</summary>
		public InteractiveObjectBuilder SetScale(float x, float y) { _scale = new Vector2(x, y); return this; }

		/// <summary>Sets the Z-index for ordering within the layer.</summary>
		public InteractiveObjectBuilder SetZIndex(int z) { _zIndex = z; return this; }

		/// <summary>Sets whether the object responds to input. Default true.</summary>
		public InteractiveObjectBuilder SetEnabled(bool enabled) { _enabled = enabled; return this; }

		/// <summary>Sets whether the object is visible. Default true.</summary>
		public InteractiveObjectBuilder SetVisible(bool visible) { _visible = visible; return this; }

		/// <summary>Sets the mouse cursor shape (Godot CursorShape enum name, e.g. "PointingHand").</summary>
		public InteractiveObjectBuilder SetCursor(string cursor) { _cursor = cursor; return this; }

		/// <summary>Sets a rectangular hit area. This is the default.</summary>
		public InteractiveObjectBuilder SetHitRect() { _hitType = HitAreaType.Rect; return this; }

		/// <summary>Sets a circular hit area with the given normalised radius (0..0.5).</summary>
		public InteractiveObjectBuilder SetHitCircle(float radius = 0.5f) { _hitType = HitAreaType.Circle; _hitRadius = radius; return this; }

		/// <summary>Sets a polygon hit area from an array of normalised vertices (0..1).</summary>
		public InteractiveObjectBuilder SetHitPolygon(Vector2[] vertices) { _hitType = HitAreaType.Polygon; _hitVertices = vertices; return this; }

		/// <summary>Configures the highlight effect colour, pulse scale and animation duration.</summary>
		public InteractiveObjectBuilder WithHighlight(Color color, float scale = 1.02f, float duration = 0.15f)
		{
			_highlightColor = color;
			_highlightScale = scale;
			_highlightDuration = duration;
			return this;
		}

		/// <summary>Sets the click callback. Receives the object's ID as parameter.</summary>
		public InteractiveObjectBuilder OnClick(Action<string> callback) { _onClick = callback; return this; }

		/// <summary>Sets the hover-enter callback. Receives the DSL command string.</summary>
		public InteractiveObjectBuilder OnHoverEnter(Action<string> callback) { _onHoverEnter = callback; return this; }

		/// <summary>Sets the hover-leave callback. Receives the DSL command string.</summary>
		public InteractiveObjectBuilder OnHoverLeave(Action<string> callback) { _onHoverLeave = callback; return this; }

		/// <summary>Sets the inline DSL command executed on hover enter.</summary>
		public InteractiveObjectBuilder SetHoverEnterDsl(string dsl) { _onHoverEnterDsl = dsl; return this; }

		/// <summary>Sets the inline DSL command executed on hover leave.</summary>
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

			var contentSize = viewBox.Content?.Size ?? new Vector2(1920f, 1024f);

			var obj = new InteractiveObject
			{
				Name = "Interactive_" + _id,
				ID = _id
			};

			layerContainer.AddChild(obj);

			var pixelPos = _position * contentSize;
			var pixelSize = _size * contentSize;
			var anchorOffset = _anchor * pixelSize;
			obj.Position = pixelPos - anchorOffset;
			obj.Size = pixelSize;
			obj.ZIndex = _zIndex;
			obj.Visible = _visible;
			obj.IsEnabled = _enabled;

			var def = new InteractiveDefinition
			{
				ID = _id,
				Texture = _texture,
				Layer = _layer,
				Size = _size,
				Anchor = _anchor,
				Scale = _scale,
				ZIndex = _zIndex,
				Enabled = _enabled,
				Visible = _visible,
				Cursor = _cursor,
				HitArea = new HitAreaDefinition
				{
					Type = _hitType.ToString().ToLowerInvariant(),
					Radius = _hitRadius,
					Vertices = _hitVertices
				},
				Highlight = new HighlightDefinition
				{
					Color = _highlightColor,
					Scale = _highlightScale,
					Duration = _highlightDuration
				}
			};

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
	}
}
