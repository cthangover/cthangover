using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Walks every mod's JSON entities the same way the
    /// runtime resolvers do — top-level <c>{"Items": [...]}</c> envelopes with
    /// <c>${include}</c> expansion for items, quests, recipes, status effects and
    /// characters, recursive scene and interactive definitions, and raw asset
    /// files for backgrounds and avatars — and records declarations and
    /// references in the shared graph. It also validates manifest
    /// <c>ui</c>/<c>main_menu</c> <c>scene_path</c> entries against the owning
    /// mod's files and turns <c>ui[].scene</c> into a scene reference.
    ///
    /// Extraction is intentionally schema-light (raw <see cref="JsonDocument"/>
    /// plus case-insensitive reads) so it keeps working when DTOs gain fields
    /// and so it can run on the background thread without touching Godot types
    /// that some DTOs embed.
    /// </summary>
    public class JsonEntityStrategy : IValidationStrategy
    {
        private static readonly HashSet<string> EffectExtensions = new(StringComparer.OrdinalIgnoreCase) { ".tscn", ".scn" };

        public string Id => "json_entities";
        public ValidationPhase Phase => ValidationPhase.Contracts;
        public int Order => 100;
        public ValidationFeature Feature => ValidationFeature.Json;

        public void Validate(ValidationContext context)
        {
            foreach (var mod in context.Mods)
            {
                context.ThrowIfCancellationRequested();

                var provider = mod.FileProvider;
                if (provider == null)
                    continue;

                CollectItems(context, mod, provider);
                CollectQuests(context, mod, provider);
                CollectRecipes(context, mod, provider);
                CollectStatusEffects(context, mod, provider);
                CollectCharacters(context, mod, provider);
                CollectSceneDefinitions(context, mod, provider);
                CollectEnvelopes(context, mod, provider, "interactives", true, ValidationKind.Interactive);

                var textureExtensions = ModConfig.Instance.GetTextureExtensionSet();
                CollectAssets(context, mod, provider, "backgrounds", ValidationKind.Background, textureExtensions);
                CollectAssets(context, mod, provider, "avatars", ValidationKind.Avatar, textureExtensions);

                var audioExtensions = new HashSet<string>(ModConfig.Instance.AudioExtensions, StringComparer.OrdinalIgnoreCase);
                CollectAssets(context, mod, provider, "music", ValidationKind.Music, audioExtensions);
                CollectAssets(context, mod, provider, "sounds", ValidationKind.Sound, audioExtensions);
                CollectAssets(context, mod, provider, "effects", ValidationKind.Effect, EffectExtensions);

                ValidateManifestEntries(context, mod, provider);
            }
        }

        private static void CollectItems(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            var documents = new List<JsonDocument>();
            var items = new List<JsonElement>();
            try
            {
                OpenEnvelopes(context, mod, provider, "items", false, documents, items);

                foreach (var item in items)
                {
                    var id = JsonReadHelper.GetString(item, "Id", "ID", "id");
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    context.Graph.AddDeclaration(ValidationKind.Item, id, mod.Id, "items", 0);
                    CollectObjectKeys(context.Graph, item);
                }
            }
            finally
            {
                DisposeAll(documents);
            }
        }

        private static void CollectQuests(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            CollectEnvelopes(context, mod, provider, "quests", false, ValidationKind.Quest);
        }

        private static void CollectStatusEffects(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            CollectEnvelopes(context, mod, provider, "status_effects", false, ValidationKind.StatusEffect);
        }

        private static void CollectRecipes(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            var documents = new List<JsonDocument>();
            var items = new List<JsonElement>();
            try
            {
                OpenEnvelopes(context, mod, provider, "recipes", false, documents, items);

                foreach (var item in items)
                {
                    var id = JsonReadHelper.GetString(item, "Id", "ID", "id");
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    context.Graph.AddDeclaration(ValidationKind.Recipe, id, mod.Id, "recipes", 0);
                    CollectIngredientReferences(context, mod, item, "Input");
                    CollectIngredientReferences(context, mod, item, "Output");
                }
            }
            finally
            {
                DisposeAll(documents);
            }
        }

        private static void CollectCharacters(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            var documents = new List<JsonDocument>();
            var items = new List<JsonElement>();
            try
            {
                OpenEnvelopes(context, mod, provider, "characters", false, documents, items);

                foreach (var item in items)
                {
                    var id = JsonReadHelper.GetString(item, "Id", "ID", "id");
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    var values = JsonReadHelper.GetProperty(item, "Values");
                    var isAction = JsonReadHelper.IsArray(values);
                    var kind = isAction ? ValidationKind.Action : ValidationKind.Character;

                    context.Graph.AddDeclaration(kind, id, mod.Id, "characters", 0);
                    CollectObjectKeys(context.Graph, item);

                    if (isAction)
                        CollectActionKeys(context.Graph, values.Value);

                    CollectLootReferences(context, mod, item);
                    CollectActionReferences(context, mod, item);
                }
            }
            finally
            {
                DisposeAll(documents);
            }
        }

        private static void CollectSceneDefinitions(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            foreach (var file in ModFileReader.ListFiles(provider, "scenes", true))
            {
                context.ThrowIfCancellationRequested();
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var document = ReadDocument(context, mod, file);
                if (document == null)
                    continue;

                using (document)
                {
                    var name = JsonReadHelper.GetString(document.RootElement, "name", "Name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        context.AddWarning("SCHEMA_MISSING_FIELD", "scene definition has no 'name'", mod.Id, file);
                        continue;
                    }

                    context.Graph.AddDeclaration(ValidationKind.Scene, name, mod.Id, file, 0);
                    CollectBackgroundReferences(context, mod, document.RootElement, file);
                }
            }
        }

        private static void CollectEnvelopes(
            ValidationContext context,
            IModInfo mod,
            IModFileProvider provider,
            string directory,
            bool recursive,
            string kind)
        {
            var documents = new List<JsonDocument>();
            var items = new List<JsonElement>();
            try
            {
                OpenEnvelopes(context, mod, provider, directory, recursive, documents, items);

                foreach (var item in items)
                {
                    var id = JsonReadHelper.GetString(item, "Id", "ID", "id");
                    context.Graph.AddDeclaration(kind, id, mod.Id, directory, 0);
                }
            }
            finally
            {
                DisposeAll(documents);
            }
        }

        private static void CollectAssets(
            ValidationContext context,
            IModInfo mod,
            IModFileProvider provider,
            string directory,
            string kind,
            HashSet<string> extensions)
        {
            foreach (var file in ModFileReader.ListFiles(provider, directory, true))
            {
                context.ThrowIfCancellationRequested();
                if (!ModFileReader.HasExtension(file, extensions))
                    continue;

                var prefixLength = directory.Length + 1;
                var relative = file.Length > prefixLength ? file.Substring(prefixLength) : file;
                var dot = relative.LastIndexOf('.');
                if (dot <= 0)
                    continue;

                context.Graph.AddDeclaration(kind, relative.Substring(0, dot), mod.Id, file, 0);
            }
        }

        private static void ValidateManifestEntries(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            var manifest = (mod as ModInfo)?.Manifest;
            if (manifest == null)
                return;

            if (manifest.Ui != null)
            {
                foreach (var entry in manifest.Ui)
                {
                    if (entry == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(entry.ScenePath) && !provider.FileExists(entry.ScenePath))
                        context.AddError("REF_NOT_FOUND",
                            $"ui scene_path '{entry.ScenePath}' not found in mod '{mod.Id}'",
                            mod.Id, "manifest.json", 0, "fix scene_path or add the scene file");

                    if (!string.IsNullOrWhiteSpace(entry.Scene)
                        && !string.Equals(entry.Scene, "shell", StringComparison.OrdinalIgnoreCase))
                        context.Graph.AddReference(ValidationKind.Scene, entry.Scene, mod.Id, "manifest.json", 0);
                }
            }

            if (manifest.MainMenu != null)
            {
                foreach (var entry in manifest.MainMenu)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ScenePath))
                        continue;

                    if (!provider.FileExists(entry.ScenePath))
                        context.AddError("REF_NOT_FOUND",
                            $"main_menu scene_path '{entry.ScenePath}' not found in mod '{mod.Id}'",
                            mod.Id, "manifest.json", 0, "fix scene_path or add the scene file");
                }
            }
        }

        private static void OpenEnvelopes(
            ValidationContext context,
            IModInfo mod,
            IModFileProvider provider,
            string directory,
            bool recursive,
            List<JsonDocument> documents,
            List<JsonElement> items)
        {
            foreach (var file in ModFileReader.ListFiles(provider, directory, recursive))
            {
                context.ThrowIfCancellationRequested();
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var document = ReadDocument(context, mod, file);
                if (document == null)
                    continue;

                var envelope = JsonReadHelper.GetProperty(document.RootElement, "Items");
                if (!JsonReadHelper.IsArray(envelope))
                {
                    document.Dispose();
                    continue;
                }

                documents.Add(document);
                foreach (var item in envelope.Value.EnumerateArray())
                    items.Add(item);
            }
        }

        private static JsonDocument ReadDocument(ValidationContext context, IModInfo mod, string file)
        {
            string text;
            try
            {
                text = ModFileReader.ReadResolved(mod.FileProvider, file);
            }
            catch (Exception ex)
            {
                context.AddError("JSON_PARSE_ERROR", $"include resolution failed: {ex.Message}", mod.Id, file);
                return null;
            }

            if (text == null)
                return null;

            try
            {
                return JsonDocument.Parse(text, JsonOptionsProvider.DocumentOptions);
            }
            catch (JsonException ex)
            {
                context.AddError("JSON_PARSE_ERROR", $"invalid JSON: {ex.Message}", mod.Id, file);
                return null;
            }
        }

        private static void CollectIngredientReferences(ValidationContext context, IModInfo mod, JsonElement entity, string field)
        {
            var ingredients = JsonReadHelper.GetProperty(entity, field);
            if (!JsonReadHelper.IsArray(ingredients))
                return;

            foreach (var ingredient in ingredients.Value.EnumerateArray())
            {
                var itemId = JsonReadHelper.GetString(ingredient, "Id", "ID", "id");
                if (!string.IsNullOrWhiteSpace(itemId))
                    context.Graph.AddReference(ValidationKind.Item, itemId, mod.Id, "recipes", 0);
            }
        }

        private static void CollectLootReferences(ValidationContext context, IModInfo mod, JsonElement entity)
        {
            var loot = JsonReadHelper.GetProperty(entity, "Loot");
            if (!JsonReadHelper.IsArray(loot))
                return;

            foreach (var drop in loot.Value.EnumerateArray())
            {
                var itemId = JsonReadHelper.GetString(drop, "ItemId");
                if (!string.IsNullOrWhiteSpace(itemId))
                    context.Graph.AddReference(ValidationKind.Item, itemId, mod.Id, "characters", 0);
            }
        }

        private static void CollectActionReferences(ValidationContext context, IModInfo mod, JsonElement entity)
        {
            var actions = JsonReadHelper.GetProperty(entity, "Actions");
            if (!JsonReadHelper.IsArray(actions))
                return;

            foreach (var action in actions.Value.EnumerateArray())
            {
                if (action.ValueKind != JsonValueKind.String)
                    continue;

                var actionId = action.GetString();
                if (!string.IsNullOrWhiteSpace(actionId))
                    context.Graph.AddReference(ValidationKind.Action, actionId, mod.Id, "characters", 0);
            }
        }

        private static void CollectBackgroundReferences(ValidationContext context, IModInfo mod, JsonElement root, string file)
        {
            var background = JsonReadHelper.GetProperty(root, "defaultBackground", "DefaultBackground");
            if (background == null)
                return;

            if (background.Value.ValueKind == JsonValueKind.String)
            {
                context.Graph.AddReference(ValidationKind.Background, background.Value.GetString(), mod.Id, file, 0);
                return;
            }

            if (background.Value.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in background.Value.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                    context.Graph.AddReference(ValidationKind.Background, entry.GetString(), mod.Id, file, 0);
            }
        }

        private static void CollectObjectKeys(ValidationGraph graph, JsonElement entity)
        {
            if (entity.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in entity.EnumerateObject())
                graph.AddDeclaredKey(property.Name);
        }

        private static void CollectActionKeys(ValidationGraph graph, JsonElement values)
        {
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.String)
                    continue;

                var text = value.GetString();
                if (string.IsNullOrEmpty(text))
                    continue;

                var separator = text.IndexOf('=');
                if (separator > 0)
                    graph.AddDeclaredKey(text.Substring(0, separator));
            }
        }

        private static void DisposeAll(List<JsonDocument> documents)
        {
            foreach (var document in documents)
                document.Dispose();
        }
    }
}
