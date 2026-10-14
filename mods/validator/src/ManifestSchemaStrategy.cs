using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Schema. Validates the mandatory shape of every mod's
    /// <c>manifest.json</c>: <c>id</c> and <c>version</c> must be present and be
    /// strings, and <c>id</c> must not be empty. The runtime silently falls back
    /// to the filesystem entry name for a missing id and tolerates a missing
    /// version, so these are exactly the problems an author should fix before
    /// publishing.
    /// </summary>
    public class ManifestSchemaStrategy : IValidationStrategy
    {
        public string Id => "manifest_schema";
        public ValidationPhase Phase => ValidationPhase.Schema;
        public int Order => 100;
        public ValidationFeature Feature => ValidationFeature.Json;

        /// <summary>Checks the required manifest.json fields: non-empty string id and string version.</summary>
        public void Validate(ValidationContext context)
        {
            foreach (var mod in context.Mods)
            {
                context.ThrowIfCancellationRequested();

                var text = mod.FileProvider?.ReadFileText("manifest.json");
                if (string.IsNullOrWhiteSpace(text))
                {
                    context.AddError("SCHEMA_MISSING_FIELD", "manifest.json is missing or empty", mod.Id, "manifest.json");
                    continue;
                }

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(text, JsonOptionsProvider.DocumentOptions);
                }
                catch (JsonException ex)
                {
                    context.AddError("JSON_PARSE_ERROR", $"manifest.json is not valid JSON: {ex.Message}", mod.Id, "manifest.json");
                    continue;
                }

                using (document)
                {
                    ValidateStringField(context, mod.Id, document.RootElement, "id", required: true);
                    ValidateStringField(context, mod.Id, document.RootElement, "version", required: true);
                    ValidateStringField(context, mod.Id, document.RootElement, "name", required: false);
                }
            }
        }

        private static void ValidateStringField(ValidationContext context, string modId, JsonElement root, string field, bool required)
        {
            var value = JsonReadHelper.GetProperty(root, field);

            if (value == null)
            {
                if (required)
                    context.AddError("SCHEMA_MISSING_FIELD", $"manifest.json has no '{field}'", modId, "manifest.json");
                return;
            }

            if (value.Value.ValueKind != JsonValueKind.String)
            {
                context.AddError("SCHEMA_WRONG_TYPE", $"'{field}' must be a string", modId, "manifest.json");
                return;
            }

            if (required && string.IsNullOrWhiteSpace(value.Value.GetString()))
                context.AddError("SCHEMA_MISSING_FIELD", $"'{field}' must not be empty", modId, "manifest.json");
        }
    }
}
