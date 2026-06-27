using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Cthangover.Core.Interactive
{
	/// <summary>
	/// Custom JSON converter for <c>Godot.Vector2</c>. System.Text.Json cannot
	/// natively deserialise Godot's Vector2 from <c>{"x":0.72,"y":0.45}</c>
	/// because its X/Y members are fields, not properties. This converter
	/// handles the <c>{"x":...,"y":...}</c> object format used throughout
	/// interactive definition JSON files.
	/// </summary>
	public class Vector2JsonConverter : JsonConverter<Vector2>
	{
		/// <summary>Reads a JSON object with "x" and "y" number members into a Vector2.</summary>
		public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException("Expected object for Vector2");

			float x = 0f;
			float y = 0f;

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType == JsonTokenType.PropertyName)
				{
					var prop = reader.GetString();
					reader.Read();

					if (string.Equals(prop, "x", StringComparison.OrdinalIgnoreCase))
						x = reader.GetSingle();
					else if (string.Equals(prop, "y", StringComparison.OrdinalIgnoreCase))
						y = reader.GetSingle();
					else
						reader.Skip();
				}
			}

			return new Vector2(x, y);
		}

		/// <summary>Writes a Vector2 as {"x":...,"y":...}.</summary>
		public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WriteNumber("x", value.X);
			writer.WriteNumber("y", value.Y);
			writer.WriteEndObject();
		}
	}

	/// <summary>
	/// JSON converter for <c>Vector2[]</c> arrays. Each element uses the
	/// <c>Vector2JsonConverter</c> for deserialisation.
	/// </summary>
	public class Vector2ArrayJsonConverter : JsonConverter<Vector2[]>
	{
		/// <summary>Reads a JSON array of <c>{"x":...,"y":...}</c> objects into a Vector2[].</summary>
		public override Vector2[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartArray)
				throw new JsonException("Expected array for Vector2[]");

			var list = new System.Collections.Generic.List<Vector2>();
			var vecConverter = new Vector2JsonConverter();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndArray)
					break;

				list.Add(vecConverter.Read(ref reader, typeof(Vector2), options));
			}

			return list.ToArray();
		}

		/// <summary>Writes a Vector2[] as an array of {"x":...,"y":...} objects.</summary>
		public override void Write(Utf8JsonWriter writer, Vector2[] value, JsonSerializerOptions options)
		{
			var vecConverter = new Vector2JsonConverter();
			writer.WriteStartArray();
			foreach (var v in value)
				vecConverter.Write(writer, v, options);
			writer.WriteEndArray();
		}
	}
}
