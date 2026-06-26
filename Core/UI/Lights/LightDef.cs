using System.Text.Json.Serialization;
using Godot;

namespace Cthangover.Core.UI.Lights
{
    /// <summary>
    /// Serializable light definition for JSON scene lighting. Coordinates are
    /// viewport-relative (0.0–1.0) so scenes adapt to different resolutions.
    /// ToPixelPos/FromPixelPos convert between normalized space and pixel space
    /// using a reference viewport size. Color is stored as a hex string for
    /// human-readable JSON editing.
    /// </summary>
	public class LightDef
	{
		[JsonPropertyName("x")]
		public float X { get; set; }

		[JsonPropertyName("y")]
		public float Y { get; set; }

		[JsonPropertyName("radius")]
		public float Radius { get; set; }

		[JsonPropertyName("influence")]
		public float Influence { get; set; } = 1f;

		[JsonPropertyName("color")]
		public string ColorHex { get; set; } = "#ffff00";

		public Color ToColor()
		{
			return new Color(ColorHex);
		}

		public Vector2 ToPixelPos(Vector2 viewportSize)
		{
			return new Vector2(X * viewportSize.X, Y * viewportSize.Y);
		}

		public void FromPixelPos(Vector2 pixelPos, Vector2 referenceSize)
		{
			X = pixelPos.X / referenceSize.X;
			Y = pixelPos.Y / referenceSize.Y;
		}
	}
}
