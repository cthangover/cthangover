namespace Cthangover.Mods.Schemas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Every schema of the loaded mods, merged: the source of the data editor window's forms,
    /// the command palette and the schema validator. Files are the <c>schemas</c> file group
    /// of all mods, read in mod load order, and merged by the same rule the engine uses for
    /// content — the later mod wins:
    /// <list type="bullet">
    /// <item>an action or a command is replaced whole by a later schema with the same id;</item>
    /// <item>a kind is merged field by field: a later mod adds its fields and replaces a field
    ///   with the same key; its non-empty name, folder and new-id pattern win.</item>
    /// </list>
    /// So a mod describes what it adds to someone else's kind without copying their schema,
    /// and a mod that disagrees with a description replaces only that one entry.
    /// </summary>
    public class SchemaCatalog
    {
        private const string Group = "schemas";

        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        private static SchemaCatalog current;
        private static object snapshot;

        /// <summary>Actions by id (case-insensitive).</summary>
        public Dictionary<string, ActionSchema> Actions { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Commands by word (case-insensitive).</summary>
        public Dictionary<string, CommandSchema> Commands { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Kinds by kind name (case-insensitive).</summary>
        public Dictionary<string, KindSchema> Kinds { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Files that did not parse, as "mod:path: message".</summary>
        public List<string> Errors { get; } = new();

        /// <summary>
        /// The catalog of the running game, rebuilt when the file group changes (a reload, a
        /// tool that cleared the file cache after writing a schema).
        /// </summary>
        public static SchemaCatalog Current
        {
            get
            {
                var files = Files.Collect(Group);
                if (current != null && ReferenceEquals(files, snapshot))
                    return current;
                snapshot = files;
                current = Build(Read(files));
                foreach (var error in current.Errors)
                    GameLogger.Log("TOOLS", "schemas: " + error, LogLevel.Error);
                return current;
            }
        }

        /// <summary>Merges schema files given as (mod, path, JSON text) in load order.</summary>
        public static SchemaCatalog Build(IEnumerable<(string ModId, string Path, string Json)> files)
        {
            var catalog = new SchemaCatalog();
            foreach (var (modId, path, json) in files)
            {
                SchemaFile file;
                try
                {
                    file = JsonSerializer.Deserialize<SchemaFile>(json ?? string.Empty, Options);
                }
                catch (JsonException ex)
                {
                    catalog.Errors.Add($"{modId}:{path}: {ex.Message}");
                    continue;
                }
                if (file != null)
                    catalog.Merge(modId, file);
            }
            return catalog;
        }

        /// <summary>The fields of <paramref name="kind"/>, or an empty list.</summary>
        public IReadOnlyList<FieldSchema> FieldsOf(string kind)
            => kind != null && Kinds.TryGetValue(kind, out var schema) ? schema.Fields : new List<FieldSchema>();

        /// <summary>Applies one mod's file on top of what is merged so far.</summary>
        public void Merge(string modId, SchemaFile file)
        {
            foreach (var action in file.Actions?.Where(a => !string.IsNullOrWhiteSpace(a?.Id)) ?? Enumerable.Empty<ActionSchema>())
            {
                action.SourceMod = modId;
                Stamp(action.Params, modId);
                Actions[action.Id] = action;
            }
            foreach (var command in file.Commands?.Where(c => !string.IsNullOrWhiteSpace(c?.Id)) ?? Enumerable.Empty<CommandSchema>())
            {
                command.SourceMod = modId;
                Stamp(command.Positional, modId);
                Stamp(command.Params, modId);
                Commands[command.Id] = command;
            }
            foreach (var kind in file.Kinds?.Where(k => !string.IsNullOrWhiteSpace(k?.Kind)) ?? Enumerable.Empty<KindSchema>())
                MergeKind(modId, kind);
        }

        private void MergeKind(string modId, KindSchema incoming)
        {
            Stamp(incoming.Fields, modId);
            if (!Kinds.TryGetValue(incoming.Kind, out var merged))
            {
                Kinds[incoming.Kind] = incoming;
                incoming.Fields ??= new List<FieldSchema>();
                return;
            }
            if (!string.IsNullOrWhiteSpace(incoming.Name))
                merged.Name = incoming.Name;
            if (!string.IsNullOrWhiteSpace(incoming.Description))
                merged.Description = incoming.Description;
            if (!string.IsNullOrWhiteSpace(incoming.Folder))
                merged.Folder = incoming.Folder;
            if (!string.IsNullOrWhiteSpace(incoming.NewId))
                merged.NewId = incoming.NewId;
            if (incoming.Order != 0)
                merged.Order = incoming.Order;
            foreach (var field in incoming.Fields ?? new List<FieldSchema>())
            {
                var at = merged.Fields.FindIndex(f => string.Equals(f.Key, field.Key, StringComparison.OrdinalIgnoreCase));
                if (at >= 0)
                    merged.Fields[at] = field;
                else
                    merged.Fields.Add(field);
            }
        }

        private static void Stamp(List<FieldSchema> fields, string modId)
        {
            foreach (var field in fields?.Where(f => f != null) ?? Enumerable.Empty<FieldSchema>())
            {
                field.SourceMod = modId;
                Stamp(field.Of, modId);
            }
        }

        private static IEnumerable<(string, string, string)> Read(Dictionary<string, FileEntry> files)
        {
            if (files == null)
                yield break;
            var order = ModRegistry.Instance.GetOrderedModIds().Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in files.Where(p => p.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(p => order.TryGetValue(p.Value.ModId ?? string.Empty, out var i) ? i : int.MaxValue)
                         .ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                yield return (pair.Value.ModId, pair.Value.FullPath, ModManager.Instance.ReadFileText(pair.Value.ModId, pair.Value.FullPath));
            }
        }
    }
}
