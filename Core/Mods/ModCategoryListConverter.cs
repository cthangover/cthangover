using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods;

/// <summary>Preserves authored category names while enforcing only the JSON array-of-strings contract.</summary>
public sealed class ModCategoryListConverter : JsonConverter<List<string>>
{
    public override bool HandleNull => true;

    public override List<string> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ModCategories.Read(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var category in ModCategories.Normalize(value)) writer.WriteStringValue(category);
        writer.WriteEndArray();
    }
}
