namespace Cthangover.Validator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Scenes;
    using Cthangover.Mods.Dialog.Scenarios;
    using Cthangover.Mods.Schemas;

    /// <summary>
    /// The validator's reading of the schemas every mod ships (<c>schemas/*.json</c>, merged
    /// by <see cref="SchemaCatalog"/>). It replaced the table of action arguments the
    /// validator kept in code — four actions, one of them with a wrong parameter name —
    /// with descriptions that live next to the actions and that any mod may add:
    /// <list type="bullet">
    /// <item><c>action</c> lines: unknown and missing parameters, bad numbers and choices, and
    ///   graph references for every entity a parameter names (characters, quests, skills,
    ///   scenes, items…), which <see cref="ReferenceStrategy"/> reports when undeclared;</item>
    /// <item>other described commands: positional values and their references (a check's stat);</item>
    /// <item>entities of described kinds: references in fields, in list elements (a trait's
    ///   <c>Stat</c>) and in map keys (a character's <c>Stats</c>).</item>
    /// </list>
    /// Kinds no other strategy declares (shops, stats, archetypes, equipment slots…) are
    /// declared here from the mods' data, so their references can be checked and the
    /// resource atlas sees their owners and patches. References are added only for kinds
    /// that are declared at all; a kind nothing in the graph knows (a map, a tileset) is
    /// not judged. Like every strategy it reads the mods' files, never the running game.
    /// </summary>
    public class SchemaValidationStrategy : IValidationStrategy
    {
        /// <inheritdoc />
        public string Id => "schemas";

        /// <inheritdoc />
        public ValidationPhase Phase => ValidationPhase.Contracts;

        /// <inheritdoc />
        public int Order => 900;

        /// <inheritdoc />
        public ValidationFeature Feature => ValidationFeature.Json;

        /// <inheritdoc />
        public void Validate(ValidationContext context)
        {
            var catalog = ValidationSchemas.Of(context);
            foreach (var error in catalog.Errors)
                context.AddError("SCHEMA_PARSE_ERROR", error, error.Split(':')[0]);

            // Every data file is parsed once: for the declarations and for the field checks.
            var entities = context.Mods.Where(m => m.FileProvider != null).ToDictionary(m => m, m => Entities(m).ToList());
            var ownKinds = DeclareDescribedKinds(context, catalog, entities);
            var declared = new HashSet<string>(context.Graph.Declarations.Select(d => d.Kind), StringComparer.OrdinalIgnoreCase) { ValidationKind.Scene };

            foreach (var (mod, list) in entities)
            {
                context.ThrowIfCancellationRequested();
                foreach (var file in ModFileReader.ListFiles(mod.FileProvider, "scenarios", true).Where(f => f.EndsWith(".scenario", StringComparison.OrdinalIgnoreCase)))
                    CheckScenario(context, catalog, mod, file, declared, ownKinds);
                foreach (var (file, kind, entity) in list)
                {
                    if (catalog.Kinds.TryGetValue(kind, out var schema))
                        CheckEntity(context, mod, file, schema, entity, ownKinds.Contains(kind) ? declared : ownKinds);
                }
            }
        }

        private static HashSet<string> DeclareDescribedKinds(ValidationContext context, SchemaCatalog catalog, Dictionary<IModInfo, List<(string File, string Kind, JsonElement Entity)>> entities)
        {
            var known = new HashSet<string>(context.Graph.Declarations.Select(d => d.Kind), StringComparer.OrdinalIgnoreCase);
            var own = new HashSet<string>(catalog.Kinds.Keys.Where(k => !known.Contains(k)), StringComparer.OrdinalIgnoreCase);
            foreach (var (mod, list) in entities)
            {
                foreach (var (file, kind, entity) in list)
                {
                    if (own.Contains(kind) && entity.TryGetProperty("Id", out var id))
                        context.Graph.AddDeclaration(kind, id.ToString(), mod.Id, file, 0);
                }
            }
            return own;
        }

        private static void CheckScenario(ValidationContext context, SchemaCatalog catalog, IModInfo mod, string file, HashSet<string> declared, HashSet<string> ownKinds)
        {
            string text;
            try
            {
                text = ModFileReader.ReadResolved(mod.FileProvider, file);
            }
            catch (Exception)
            {
                return;
            }
            if (text == null)
                return;
            var body = ScenarioParser.ParseMetadata(text).remainingText.Split('\n');
            var offset = text.Split('\n').Length - body.Length;
            for (var i = 0; i < body.Length; i++)
            {
                var line = body[i].Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(':'))
                    continue;
                var positional = new List<string>();
                var named = new Dictionary<string, string>();
                var command = ScenarioParser.SplitLine(line, positional, named, out _);
                var lineNumber = offset + i + 1;
                if (command == "action" && positional.Count > 0 && catalog.Actions.TryGetValue(positional[0], out var action))
                {
                    Report(context, SchemaChecker.CheckNamed(action.Id, action.Params, named, null), mod.Id, file, lineNumber);
                    foreach (var field in action.Params ?? new List<FieldSchema>())
                    {
                        var value = named.FirstOrDefault(p => string.Equals(p.Key, field.Key, StringComparison.OrdinalIgnoreCase)).Value;
                        AddReferences(context, field, value, declared, mod.Id, file, lineNumber);
                    }
                }
                else if (command != null && command != "action" && catalog.Commands.TryGetValue(command, out var schema))
                {
                    Report(context, SchemaChecker.CheckPositional(command, schema.Positional, positional, null), mod.Id, file, lineNumber);
                    // The commands' own strategies already reference their scenes and resources;
                    // only kinds the schemas brought in are referenced here.
                    var fields = schema.Positional ?? new List<FieldSchema>();
                    for (var p = 0; p < fields.Count && p < positional.Count; p++)
                        AddReferences(context, fields[p], positional[p], ownKinds, mod.Id, file, lineNumber);
                }
            }
        }

        private static void CheckEntity(ValidationContext context, IModInfo mod, string file, KindSchema schema, JsonElement entity, HashSet<string> targets)
        {
            var id = entity.TryGetProperty("Id", out var idProp) ? idProp.ToString() : "?";
            var owner = $"{schema.Kind} '{id}'";
            foreach (var field in schema.Fields)
            {
                if (!TryGet(entity, field.Key, out var value))
                    continue;
                switch (field.Type?.ToLowerInvariant())
                {
                    case "list" when value.ValueKind == JsonValueKind.Array && field.Of != null:
                        foreach (var element in value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
                        {
                            foreach (var sub in field.Of)
                            {
                                if (!TryGet(element, sub.Key, out var subValue))
                                    continue;
                                Report(context, SchemaChecker.CheckValue($"{owner}.{field.Key}", sub, Text(subValue), null), mod.Id, file, 0);
                                AddReferences(context, sub, Text(subValue), targets, mod.Id, file, 0);
                            }
                        }
                        break;
                    case "map" when value.ValueKind == JsonValueKind.Object && targets.Contains(field.Kind ?? string.Empty):
                        foreach (var key in value.EnumerateObject().Select(p => p.Name))
                            context.Graph.AddReference(field.Kind, key, mod.Id, file, 0);
                        break;
                    case "ref_list" when value.ValueKind == JsonValueKind.Array:
                        AddReferences(context, field, string.Join(",", value.EnumerateArray().Select(Text)), targets, mod.Id, file, 0);
                        break;
                    default:
                        if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                        {
                            Report(context, SchemaChecker.CheckValue(owner, field, Text(value), null), mod.Id, file, 0);
                            AddReferences(context, field, Text(value), targets, mod.Id, file, 0);
                        }
                        break;
                }
            }
        }

        private static void AddReferences(ValidationContext context, FieldSchema field, string value, HashSet<string> kinds, string mod, string file, int line)
        {
            foreach (var (kind, id) in SchemaReferences.Of(field, value))
            {
                if (!kinds.Contains(kind))
                    continue;
                if (kind == ValidationKind.Scene && SceneManager.IsNativeSceneName(id))
                    continue;
                context.Graph.AddReference(kind, id, mod, file, line);
            }
        }

        private static IEnumerable<(string File, string Kind, JsonElement Entity)> Entities(IModInfo mod)
        {
            foreach (var file in ModFileReader.ListFiles(mod.FileProvider, string.Empty, true).Where(IsDataFile))
            {
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(ModFileReader.ReadResolved(mod.FileProvider, file) ?? string.Empty);
                }
                catch (Exception)
                {
                    continue;
                }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Kind", out var kind) || kind.ValueKind != JsonValueKind.String
                        || !root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var entity in items.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList())
                        yield return (file, kind.GetString(), entity.Clone());
                }
            }
        }

        private static bool IsDataFile(string path)
            => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
               && !path.StartsWith("schemas/", StringComparison.OrdinalIgnoreCase)
               && !path.StartsWith("patches/", StringComparison.OrdinalIgnoreCase)
               && !path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase);

        private static bool TryGet(JsonElement obj, string key, out JsonElement value)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();

        private static void Report(ValidationContext context, IEnumerable<SchemaIssue> issues, string mod, string file, int line)
        {
            foreach (var issue in issues)
            {
                if (issue.IsError)
                    context.AddError(issue.Code, issue.Message, mod, file, line);
                else
                    context.AddWarning(issue.Code, issue.Message, mod, file, line);
            }
        }
    }
}
