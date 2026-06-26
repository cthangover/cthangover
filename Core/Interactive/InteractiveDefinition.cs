using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Godot;

namespace Cthangover.Core.Interactive
{
	/// <summary>
	/// JSON-driven definition of an interactive scene object loaded from mods.
	/// Position, size and hit-area coordinates are normalised (0..1) relative
	/// to <c>ViewBox.Content</c> size, making them resolution-independent.
	/// Textures are resolved through <c>ModManager.ResolveTexture</c>.
	/// JSON files follow the <c>{"Items": [...]}</c> envelope for
	/// <c>ModManager.CollectJsonGroup</c> compatibility.
	/// </summary>
	public class InteractiveDefinition : IIdentifiable
	{
		/// <summary>Unique identifier used in scenario commands and registry lookups.</summary>
		[JsonPropertyName("id")]
		public string ID { get; set; }

		/// <summary>Owning mod identifier set by ModManager during collection.</summary>
		[JsonIgnore]
		public string ModId { get; set; }

		/// <summary>Texture key resolved via <c>ModManager.ResolveTexture</c> (e.g. "interactive/door").</summary>
		[JsonPropertyName("texture")]
		public string Texture { get; set; }

		/// <summary>Visual layer: "background", "foreground" or "ui". Defaults to "foreground".</summary>
		[JsonPropertyName("layer")]
		public string Layer { get; set; } = "foreground";

		/// <summary>Normalised position of the object's anchor point (0..1).</summary>
		[JsonPropertyName("position")]
		public Vector2 Position { get; set; }

		/// <summary>Normalised size of the bounding box (0..1). Used for both visual scaling and hit-area bounds.</summary>
		[JsonPropertyName("size")]
		public Vector2 Size { get; set; } = Vector2.One;

		/// <summary>Anchor point within the object (0..1). (0,0) = top-left, (0.5,0.5) = centre.</summary>
		[JsonPropertyName("anchor")]
		public Vector2 Anchor { get; set; } = new(0.5f, 0.5f);

		/// <summary>Visual scale multiplier applied to the texture. Defaults to (1,1).</summary>
		[JsonPropertyName("scale")]
		public Vector2 Scale { get; set; } = Vector2.One;

		/// <summary>Z-index for ordering within the layer. Higher = on top.</summary>
		[JsonPropertyName("zIndex")]
		public int ZIndex { get; set; }

		/// <summary>Whether the object is initially enabled (responds to input). Default true.</summary>
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; } = true;

		/// <summary>Whether the object is initially visible. Default true.</summary>
		[JsonPropertyName("visible")]
		public bool Visible { get; set; } = true;

		/// <summary>Mouse cursor override when hovering (Godot CursorShape enum name, e.g. "PointingHand").</summary>
		[JsonPropertyName("cursor")]
		public string Cursor { get; set; }

		/// <summary>Hit-area shape configuration. If omitted, the full bounding rect is used.</summary>
		[JsonPropertyName("hitArea")]
		public HitAreaDefinition HitArea { get; set; }

		/// <summary>Visual highlight settings applied on hover.</summary>
		[JsonPropertyName("highlight")]
		public HighlightDefinition Highlight { get; set; }

		/// <summary>Actions triggered by pointer events.</summary>
		[JsonPropertyName("actions")]
		public InteractiveActionDefinition Actions { get; set; }
	}

	/// <summary>
	/// Shape and dimensions of the clickable area within the object's bounding box.
	/// All coordinates are normalised within the object (0..1), where (0,0) is
	/// the top-left corner and (1,1) is the bottom-right.
	/// </summary>
	public class HitAreaDefinition
	{
		/// <summary><c>"rect"</c>, <c>"circle"</c> or <c>"polygon"</c>. Defaults to <c>"rect"</c>.</summary>
		[JsonPropertyName("type")]
		public string Type { get; set; } = "rect";

		/// <summary>Circle radius, normalised to half the smaller object dimension (0..0.5).</summary>
		[JsonPropertyName("radius")]
		public float Radius { get; set; } = 0.5f;

		/// <summary>
		/// Polygon vertices as an array of normalised Vector2 points.
		/// Only used when <c>Type</c> is <c>"polygon"</c>.
		/// </summary>
		[JsonPropertyName("vertices")]
		public Vector2[] Vertices { get; set; }
	}

	/// <summary>
	/// Highlight effect applied when the pointer hovers over the object.
	/// Uses a shader-based colour modulation on the texture.
	/// </summary>
	public class HighlightDefinition
	{
		/// <summary>Modulate colour mixed into the texture (RGBA). Alpha controls blend strength.</summary>
		[JsonPropertyName("color")]
		public Color Color { get; set; } = new(1f, 1f, 0f, 0.3f);

		/// <summary>Pulse scale multiplier applied during hover (e.g. 1.02 = 2% bigger).</summary>
		[JsonPropertyName("scale")]
		public float Scale { get; set; } = 1.02f;

		/// <summary>Duration in seconds of the hover/unhover animation.</summary>
		[JsonPropertyName("duration")]
		public float Duration { get; set; } = 0.15f;
	}

	/// <summary>
	/// Actions dispatched on pointer events.
	/// Each action can reference a <c>.scenario</c> file, inline DSL commands,
	/// or both (scenario executes first).
	/// </summary>
	public class InteractiveActionDefinition
	{
		/// <summary>Action executed on left-click. Can reference a scenario and/or inline commands.</summary>
		[JsonPropertyName("onClick")]
		public ClickAction OnClick { get; set; }

		/// <summary>Inline DSL commands executed on mouse enter (e.g. "set cursor_hint=door").</summary>
		[JsonPropertyName("onHoverEnter")]
		public string OnHoverEnter { get; set; }

		/// <summary>Inline DSL commands executed on mouse leave.</summary>
		[JsonPropertyName("onHoverLeave")]
		public string OnHoverLeave { get; set; }
	}

	/// <summary>
	/// Click action descriptor. Supports both a referenced <c>.scenario</c> file
	/// and inline DSL commands, executed in that order.
	/// </summary>
	public class ClickAction
	{
		/// <summary>Path to a <c>.scenario</c> file within a mod (e.g. "scenarios/door_clicked.scenario").</summary>
		[JsonPropertyName("scenario")]
		public string Scenario { get; set; }

		/// <summary>Inline DSL commands to execute (one command per array element).</summary>
		[JsonPropertyName("commands")]
		public string[] Commands { get; set; }
	}
}
