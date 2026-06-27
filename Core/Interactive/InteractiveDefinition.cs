using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Godot;

namespace Cthangover.Core.Interactive
{
	/// <summary>
	/// JSON-driven definition of an interactive scene object loaded from mods.
	/// The texture is always full-screen (alpha determines visibility outside the collider).
	/// The collider defines the clickable region in normalised 0..1 viewport coordinates.
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

		/// <summary>Texture key resolved via <c>ModManager.ResolveTexture</c>. Full-screen with alpha.</summary>
		[JsonPropertyName("texture")]
		public string Texture { get; set; }

		/// <summary>Visual layer: "background", "foreground" or "ui". Defaults to "foreground".</summary>
		[JsonPropertyName("layer")]
		public string Layer { get; set; } = "foreground";

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

		/// <summary>Collider shape and position in normalised viewport coordinates (0..1).</summary>
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
	/// Shape and position of the clickable collider in normalised viewport
	/// coordinates (0..1), where (0,0) is the top-left corner and (1,1) is bottom-right.
	/// </summary>
	public class HitAreaDefinition
	{
		/// <summary><c>"rect"</c>, <c>"circle"</c> or <c>"polygon"</c>. Defaults to <c>"rect"</c>.</summary>
		[JsonPropertyName("type")]
		public string Type { get; set; } = "rect";

		/// <summary>X position of the collider origin (top-left for rect, centre for circle). Normalised 0..1.</summary>
		[JsonPropertyName("x")]
		public float X { get; set; }

		/// <summary>Y position of the collider origin. Normalised 0..1.</summary>
		[JsonPropertyName("y")]
		public float Y { get; set; }

		/// <summary>Width for rect type. Normalised 0..1.</summary>
		[JsonPropertyName("width")]
		public float Width { get; set; } = 0.1f;

		/// <summary>Height for rect type. Normalised 0..1.</summary>
		[JsonPropertyName("height")]
		public float Height { get; set; } = 0.1f;

		/// <summary>Radius for circle type. Normalised 0..1 (relative to viewport min dimension).</summary>
		[JsonPropertyName("radius")]
		public float Radius { get; set; } = 0.05f;

		/// <summary>Polygon vertices as an array of normalised Vector2 points. Only for "polygon" type.</summary>
		[JsonPropertyName("vertices")]
		[JsonConverter(typeof(Vector2ArrayJsonConverter))]
		public Vector2[] Vertices { get; set; }
	}

	/// <summary>
	/// Highlight effect applied when the pointer hovers over the collider.
	/// Uses a shader-based colour modulation on the full-screen texture.
	/// </summary>
	public class HighlightDefinition
	{
		/// <summary>Modulate colour as a hex string (e.g. "#FFFF0033"). Parsed to Godot.Color at runtime.</summary>
		[JsonPropertyName("color")]
		public string ColorHex { get; set; } = "#FFFF0033";

		/// <summary>Pulse scale multiplier applied during hover (e.g. 1.02 = 2% bigger).</summary>
		[JsonPropertyName("scale")]
		public float Scale { get; set; } = 1.02f;

		/// <summary>Duration in seconds of the hover/unhover animation.</summary>
		[JsonPropertyName("duration")]
		public float Duration { get; set; } = 0.15f;

		/// <summary>Parsed Godot.Color from ColorHex. Computed at load time, not serialised.</summary>
		[JsonIgnore]
		public Color Color => ParseColor(ColorHex);

		private static Color ParseColor(string hex)
		{
			if (string.IsNullOrEmpty(hex))
				return new Color(1f, 1f, 0f, 0.3f);

			try
			{
				return new Color(hex);
			}
			catch
			{
				return new Color(1f, 1f, 0f, 0.3f);
			}
		}
	}

	/// <summary>
	/// Actions dispatched on pointer events.
	/// </summary>
	public class InteractiveActionDefinition
	{
		/// <summary>Action executed on left-click.</summary>
		[JsonPropertyName("onClick")]
		public ClickAction OnClick { get; set; }

		/// <summary>Inline DSL commands executed on mouse enter.</summary>
		[JsonPropertyName("onHoverEnter")]
		public string OnHoverEnter { get; set; }

		/// <summary>Inline DSL commands executed on mouse leave.</summary>
		[JsonPropertyName("onHoverLeave")]
		public string OnHoverLeave { get; set; }
	}

	/// <summary>
	/// Click action descriptor. Supports a referenced <c>.scenario</c> file
	/// and/or inline DSL commands.
	/// </summary>
	public class ClickAction
	{
		/// <summary>Path to a <c>.scenario</c> file within a mod.</summary>
		[JsonPropertyName("scenario")]
		public string Scenario { get; set; }

		/// <summary>Inline DSL commands to execute (one command per array element).</summary>
		[JsonPropertyName("commands")]
		public string[] Commands { get; set; }
	}
}
