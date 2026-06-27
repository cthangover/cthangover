using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Interactive;
using Godot;

namespace Cthangover.Tools.InteractiveEditor
{
	/// <summary>
	/// Data model for the interactive editor. Holds interactive definition
	/// fields, manages collider vertex list for polygon editing, and produces
	/// the output JSON.
	/// </summary>
	public class InteractiveEditorController
	{
		public string Id { get; set; } = "new_interactive";
		public string Texture { get; set; } = "";
		public string Layer { get; set; } = "foreground";
		public int ZIndex { get; set; }
		public bool Enabled { get; set; } = true;
		public bool Visible { get; set; } = true;
		public string Cursor { get; set; } = "";
		public string HitType { get; set; } = "rect";

		public float RectX { get; set; }
		public float RectY { get; set; }
		public float RectW { get; set; } = 0.1f;
		public float RectH { get; set; } = 0.1f;

		public float CircleX { get; set; } = 0.5f;
		public float CircleY { get; set; } = 0.5f;
		public float CircleRadius { get; set; } = 0.05f;

		public List<Vector2> PolygonVertices { get; set; } = new();

		public string HighlightColorHex { get; set; } = "#FFFF0033";
		public float HighlightScale { get; set; } = 1.02f;

		public string OnClickScenario { get; set; } = "";
		public string OnClickCommands { get; set; } = "";
		public string OnHoverEnter { get; set; } = "";
		public string OnHoverLeave { get; set; } = "";

		/// <summary>Builds an InteractiveDefinition from current state.</summary>
		public InteractiveDefinition ToDefinition()
		{
			var hitArea = new HitAreaDefinition { Type = HitType };

			switch (HitType)
			{
				case "rect":
					hitArea.X = RectX;
					hitArea.Y = RectY;
					hitArea.Width = RectW;
					hitArea.Height = RectH;
					break;
				case "circle":
					hitArea.X = CircleX;
					hitArea.Y = CircleY;
					hitArea.Radius = CircleRadius;
					break;
				case "polygon":
					hitArea.Vertices = PolygonVertices.ToArray();
					break;
			}

			var clickAction = new ClickAction();

			if (!string.IsNullOrWhiteSpace(OnClickScenario))
				clickAction.Scenario = OnClickScenario.Trim();

			if (!string.IsNullOrWhiteSpace(OnClickCommands))
			{
				var lines = OnClickCommands.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
				var cleanLines = new List<string>();
				foreach (var line in lines)
				{
					var trimmed = line.Trim();
					if (!string.IsNullOrEmpty(trimmed))
						cleanLines.Add(trimmed);
				}
				if (cleanLines.Count > 0)
					clickAction.Commands = cleanLines.ToArray();
			}

			return new InteractiveDefinition
			{
				ID = Id,
				Texture = string.IsNullOrWhiteSpace(Texture) ? null : Texture.Trim(),
				Layer = Layer,
				ZIndex = ZIndex,
				Enabled = Enabled,
				Visible = Visible,
				Cursor = string.IsNullOrWhiteSpace(Cursor) ? null : Cursor.Trim(),
				HitArea = hitArea,
				Highlight = new HighlightDefinition
				{
					ColorHex = HighlightColorHex,
					Scale = HighlightScale
				},
				Actions = new InteractiveActionDefinition
				{
					OnClick = (clickAction.Scenario != null || clickAction.Commands != null) ? clickAction : null,
					OnHoverEnter = string.IsNullOrWhiteSpace(OnHoverEnter) ? null : OnHoverEnter.Trim(),
					OnHoverLeave = string.IsNullOrWhiteSpace(OnHoverLeave) ? null : OnHoverLeave.Trim()
				}
			};
		}

		/// <summary>Loads state from an InteractiveDefinition.</summary>
		public void FromDefinition(InteractiveDefinition def)
		{
			if (def == null) return;

			Id = def.ID ?? "new_interactive";
			Texture = def.Texture ?? "";
			Layer = def.Layer ?? "foreground";
			ZIndex = def.ZIndex;
			Enabled = def.Enabled;
			Visible = def.Visible;
			Cursor = def.Cursor ?? "";

			if (def.HitArea != null)
			{
				HitType = def.HitArea.Type ?? "rect";
				RectX = def.HitArea.X;
				RectY = def.HitArea.Y;
				RectW = def.HitArea.Width;
				RectH = def.HitArea.Height;
				CircleX = def.HitArea.X;
				CircleY = def.HitArea.Y;
				CircleRadius = def.HitArea.Radius;
				PolygonVertices = def.HitArea.Vertices != null
					? new List<Vector2>(def.HitArea.Vertices)
					: new List<Vector2>();
			}
			else
			{
				HitType = "rect";
			}

			if (def.Highlight != null)
			{
				HighlightColorHex = def.Highlight.ColorHex ?? "#FFFF0033";
				HighlightScale = def.Highlight.Scale;
			}

			OnClickScenario = def.Actions?.OnClick?.Scenario ?? "";
			OnClickCommands = def.Actions?.OnClick?.Commands != null
				? string.Join("\n", def.Actions.OnClick.Commands)
				: "";
			OnHoverEnter = def.Actions?.OnHoverEnter ?? "";
			OnHoverLeave = def.Actions?.OnHoverLeave ?? "";
		}

		/// <summary>Serializes the definition to formatted JSON (FileData envelope).</summary>
		public string ToJson()
		{
			var def = ToDefinition();
			var wrapper = new
			{
				Items = new[] { def }
			};

			var options = new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};

			return JsonSerializer.Serialize(wrapper, options);
		}

		/// <summary>Parses JSON back into the controller. Returns false on failure.</summary>
		public bool FromJson(string json)
		{
			try
			{
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var wrapper = JsonSerializer.Deserialize<FileDataWrapper>(json, options);
				if (wrapper?.Items != null && wrapper.Items.Count > 0)
				{
					FromDefinition(wrapper.Items[0]);
					return true;
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		private class FileDataWrapper
		{
			public List<InteractiveDefinition> Items { get; set; }
		}

		/// <summary>Updates a polygon vertex at the given index.</summary>
		public void SetPolygonVertex(int index, Vector2 value)
		{
			while (PolygonVertices.Count <= index)
				PolygonVertices.Add(Vector2.Zero);
			PolygonVertices[index] = value;
		}

		/// <summary>Removes a polygon vertex at the given index.</summary>
		public void RemovePolygonVertex(int index)
		{
			if (index >= 0 && index < PolygonVertices.Count)
				PolygonVertices.RemoveAt(index);
		}

		/// <summary>Adds a new polygon vertex at the default position (center of viewport).</summary>
		public void AddPolygonVertex()
		{
			PolygonVertices.Add(new Vector2(0.5f, 0.5f));
		}
	}
}
