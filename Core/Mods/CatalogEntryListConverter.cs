using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods;

/// <summary>A JSON type error belongs to one catalog record, not to every other mod in the response.</summary>
public sealed class CatalogEntryListConverter : JsonConverter<List<CatalogExportEntry>>
{
    public override List<CatalogExportEntry> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("mods must be a JSON array of records");
        var entries = new List<CatalogExportEntry>();
        foreach (var value in document.RootElement.EnumerateArray())
        {
            try
            {
                if (value.ValueKind != JsonValueKind.Object) throw new JsonException("record must be a JSON object");
                var entry = JsonSerializer.Deserialize<CatalogExportEntry>(value.GetRawText(), options);
                if (entry != null) entries.Add(entry);
            }
            catch (JsonException error)
            {
                var id = value.ValueKind == JsonValueKind.Object && value.TryGetProperty("id", out var name)
                    && name.ValueKind == JsonValueKind.String ? name.GetString() : "?";
                GameLogger.Log("CATALOG", $"Catalog record '{id}' has a JSON type error and was skipped: {error.Message}", LogLevel.Warning);
            }
        }
        return entries;
    }

    public override void Write(Utf8JsonWriter writer, List<CatalogExportEntry> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var entry in value) JsonSerializer.Serialize(writer, entry, options);
        writer.WriteEndArray();
    }
}
