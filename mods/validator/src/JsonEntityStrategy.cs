using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Walks every mod's JSON entities the same way the
    /// runtime resolver does — every <c>.json</c> file is opened and routed by
    /// its top-level <c>"Kind"</c>, with <c>${include}</c> expansion applied —
    /// and records declarations and references in the shared graph. Files
    /// without a kind (manifests, collections, live2d models, scene
    /// definitions, patches) are not entity data and are skipped; scene and
    /// interactive definitions plus raw asset files keep their directory-based
    /// collection. It also validates manifest <c>ui</c>/<c>main_menu</c>
    /// <c>scene_path</c> entries against the owning mod's files and turns
    /// <c>ui[].scene</c> into a scene reference.
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

        /// <summary>Walks every mod's JSON entities, scene definitions, interactives and texture assets, recording declarations and references into the fact graph.</summary>
        public void Validate(ValidationContext context)
        {
            foreach (var mod in context.Mods)
            {
                context.ThrowIfCancellationRequested();

                var provider = mod.FileProvider;
                if (provider == null)
                    continue;

                CollectEntities(context, mod, provider);
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

        /// <summary>
        /// Scans every JSON file of the mod and routes it by its declared
        /// <c>"Kind"</c>, mirroring the runtime <c>EntityResolver</c>. The kind
        /// resolves to an <see cref="IEntityKindStrategy"/> that records the
        /// declarations, declared keys and references for that entity type — so
        /// an action can no longer be mistaken for a character just because both
        /// lived in the same directory, and a new kind is a new strategy class
        /// instead of a new switch case.
        /// </summary>
        private static void CollectEntities(ValidationContext context, IModInfo mod, IModFileProvider provider)
        {
            foreach (var file in ModFileReader.ListFiles(provider, string.Empty, true))
            {
                context.ThrowIfCancellationRequested();
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                // The reserved collections/ folder holds scheme documents whose
                // "kind" (game/component/toolset) is not an entity discriminator.
                if (file.StartsWith(ModCollectionStore.ProviderDirectory + "/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var document = ReadDocument(context, mod, file);
                if (document == null)
                    continue;

                using (document)
                {
                    var kind = JsonReadHelper.GetString(document.RootElement, "Kind", "kind");
                    if (string.IsNullOrWhiteSpace(kind))
                        continue;

                    var envelope = JsonReadHelper.GetProperty(document.RootElement, "Items");
                    if (!JsonReadHelper.IsArray(envelope))
                        continue;

                    var strategy = EntityKindStrategyRegistry.Resolve(kind);
                    if (strategy == null)
                    {
                        context.AddWarning("UNKNOWN_ENTITY_KIND",
                            $"unknown entity kind '{kind}'",
                            mod.Id, file, 0);
                        continue;
                    }

                    strategy.Collect(context, mod, file, envelope.Value);
                }
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

        private static void DisposeAll(List<JsonDocument> documents)
        {
            foreach (var document in documents)
                document.Dispose();
        }
    }
}
