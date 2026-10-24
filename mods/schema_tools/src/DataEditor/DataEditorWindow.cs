namespace Cthangover.Tools.Schemas
{
    using Cthangover.Mods.Schemas;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Tools.Common;
    using Cthangover.Tools.EntityEditor;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// The data editor (RPG Maker's F9 "Database", built from schemas instead of fixed tabs): every entity kind some mod described
    /// in a schema — stats, archetypes, items, characters, battle actions, shops, equipment
    /// slots — on the left, the entities of the chosen kind in one writable mod in the
    /// middle, and a form on the right built from the merged fields of all mods' schemas.
    /// <para/>
    /// That merge is the point: the items mod describes price and picture, the equipment
    /// mod adds "Equip" and "Traits", the stats add "Archetype" and "Stats" to characters,
    /// and one form shows them all; a mod that adds a field to any kind gets an editor for
    /// it by shipping a schema file, without touching this window or the kind's owner.
    /// Properties no schema describes stay editable as JSON under "other fields" and are
    /// written back as they were.
    /// <para/>
    /// Loading and saving reuse the data editors' <see cref="EntityDocument"/>: files are kept
    /// as JSON trees, only changed files are written, texts go into the mod's locale files,
    /// and Save drops the game's caches so the running game sees the change at once.
    /// </summary>
    public partial class DataEditorWindow : ToolWindow
    {
        private readonly List<string> mods = new();
        private readonly List<ISchemaFieldEditor> editors = new();
        private readonly List<LocaleFieldEditor> localeEditors = new();
        private OptionButton modPicker;
        private FilteredList kindList;
        private FilteredList entityList;
        private VBoxContainer form;
        private Label kindLabel;
        private Label fileLabel;
        private LineEdit idEdit;
        private CodeEdit otherEdit;
        private Label emptyHint;
        private KindSchema kind;
        private EntityDocument document;
        private EntityRecord selected;
        private List<string> languages = new();
        private string modId;
        private bool suppress;
        private bool editing;

        /// <summary>Builds the window over the current schema catalog.</summary>
        public DataEditorWindow() : base("tools/schema/data_editor/title")
        {
            CthangoverTheme.Apply(this);
            BuildUi();
            FillMods();
            FillKinds();
            SetHint(ToolUi.Tr("tools/schema/data_editor/hint"));
        }

        /// <summary>The kind shown, or null.</summary>
        public string KindId => kind?.Kind;

        /// <summary>The edited mod.</summary>
        public string ModId => modId;

        /// <summary>The selected entity's id, or null.</summary>
        public string SelectedId => selected?.Id;

        /// <summary>The edited entities of the kind in the mod.</summary>
        public EntityDocument Document => document;

        /// <summary>Shows <paramref name="kindName"/> (asks about unsaved changes first).</summary>
        public void OpenKind(string kindName)
        {
            RunDiscardingChanges(() => DoOpen(kindName, modId), () => kindList.Select(kind?.Kind));
        }

        /// <summary>Shows the kind in another mod (asks about unsaved changes first).</summary>
        public void OpenMod(string mod)
        {
            RunDiscardingChanges(() => DoOpen(kind?.Kind, mod), () => modPicker.Select(mods.IndexOf(modId)));
        }

        /// <summary>Selects an entity by id (null: none).</summary>
        public void Select(string id)
        {
            selected = id == null ? null : document?.Find(id);
            entityList.Select(selected?.Id);
            ShowSelected();
        }

        /// <summary>The editor of a field by key (tests drive the form through it).</summary>
        public ISchemaFieldEditor Editor(string key) => editors.FirstOrDefault(e => string.Equals(e.Field.Key, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>Copies the form into the selected entity (what every edit does).</summary>
        public void ApplyForm() => WriteForm();

        // ── layout ─────────────────────────────────────────────────

        private void BuildUi()
        {
            var outer = CreateFillContainer();
            AddChild(outer);
            var toolbar = CreateToolbar();
            modPicker = new OptionButton { TooltipText = ToolUi.Tr("tools/entity_editor/mod_tip") };
            modPicker.ItemSelected += OnModPicked;
            toolbar.AddChild(modPicker);
            toolbar.AddChild(ToolUi.Button("tools/common/save", OnSavePressed, ButtonKind.Primary, "tools/common/save_tip"));
            toolbar.AddChild(ToolUi.Divider());
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/new", AddNew, ButtonKind.Secondary, "tools/entity_editor/new_tip"));
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/duplicate", DuplicateSelected, ButtonKind.Secondary, "tools/entity_editor/duplicate_tip"));
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/delete", DeleteSelected, ButtonKind.Secondary, "tools/entity_editor/delete_tip"));
            toolbar.AddChild(ToolUi.Divider());
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/undo", Undo, ButtonKind.Secondary, "tools/entity_editor/undo_tip"));
            toolbar.AddChild(ToolUi.Button("tools/entity_editor/redo", Redo, ButtonKind.Secondary, "tools/entity_editor/redo_tip"));
            outer.AddChild(toolbar);

            var main = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            outer.AddChild(main);
            var kinds = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            kinds.AddChild(ToolUi.Section("tools/schema/data_editor/section_kinds"));
            kindList = new FilteredList(300f, "tools/schema/data_editor/search_kind") { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            kindList.Chosen += OpenKind;
            kinds.AddChild(kindList);
            main.AddChild(CreateSidebar(kinds, 230));

            var right = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            main.AddChild(right);
            var entities = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            entities.AddChild(ToolUi.Section("tools/entity_editor/section_list"));
            entityList = new FilteredList(300f, "tools/entity_editor/search") { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            entityList.Chosen += OnEntityChosen;
            entities.AddChild(entityList);
            right.AddChild(CreateSidebar(entities, 280));

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            right.AddChild(scroll);
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", UiTokens.Space2);
            scroll.AddChild(column);
            kindLabel = ToolUi.Hint(string.Empty);
            column.AddChild(kindLabel);
            emptyHint = ToolUi.Hint(ToolUi.Tr("tools/entity_editor/no_entity"));
            column.AddChild(emptyHint);
            form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Name = "Form" };
            form.AddThemeConstantOverride("separation", UiTokens.Space2);
            column.AddChild(form);
        }

        private void FillMods()
        {
            mods.Clear();
            mods.AddRange(ModFileService.ListWritableMods());
            modPicker.Clear();
            foreach (var mod in mods)
                modPicker.AddItem(mod);
            if (mods.Count == 0)
                SetStatus(ToolUi.Tr("tools/common/no_writable_mods"), ToolStatusKind.Error);
            else
                modId = mods[0];
            modPicker.Select(mods.IndexOf(modId));
        }

        private void FillKinds()
        {
            var counts = GameEntityIndex.Kinds();
            var kinds = SchemaCatalog.Current.Kinds.Values.Where(k => k.Fields.Count > 0)
                .OrderBy(k => k.Order).ThenBy(k => k.Kind, StringComparer.Ordinal).ToList();
            kindList.SetItems(kinds.Select(k => (k.Kind, $"{KindName(k)}  ·  {(counts.TryGetValue(k.Kind, out var n) ? n : 0)}", (Texture2D)null)));
            SetStatus(string.Format(ToolUi.Tr("tools/schema/data_editor/kinds"), kinds.Count));
        }

        private static string KindName(KindSchema k) => string.IsNullOrEmpty(k.Name) ? k.Kind : ToolUi.Tr(k.Name);

        // ── kind and mod ───────────────────────────────────────────

        private void DoOpen(string kindName, string mod)
        {
            if (kindName == null || mod == null || !SchemaCatalog.Current.Kinds.TryGetValue(kindName, out var schema))
                return;
            kind = schema;
            modId = mod;
            modPicker.Select(mods.IndexOf(mod));
            kindList.Select(kind.Kind);
            kindLabel.Text = string.IsNullOrEmpty(kind.Description) ? KindName(kind) : $"{KindName(kind)} — {ToolUi.Tr(kind.Description)}";
            var sources = ModFileService.ListFiles(mod, string.Empty, ".json").Select(path => (path, ModFileService.ReadText(mod, path)));
            document = EntityDocument.Load(kind.Kind, $"{Folder()}/{mod}.json", sources, out var skipped);
            if (skipped.Count > 0)
                SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/broken_files"), string.Join(", ", skipped)), ToolStatusKind.Error);
            languages = LocaleFileService.Languages(mod);
            BuildForm();
            MarkClean();
            editing = false;
            RefreshList();
            selected = document.Records.FirstOrDefault();
            entityList.Select(selected?.Id);
            ShowSelected();
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/opened"), document.Records.Count, mod));
        }

        private string Folder() => string.IsNullOrWhiteSpace(kind?.Folder) ? kind?.Kind?.Replace('/', '_') : kind.Folder;

        private void OnModPicked(long index)
        {
            if (index >= 0 && index < mods.Count && mods[(int)index] != modId)
                OpenMod(mods[(int)index]);
        }

        private void OnEntityChosen(string id) => Select(id);

        private void RefreshList()
        {
            var name = editors.OfType<LocaleFieldEditor>().FirstOrDefault();
            entityList.SetItems(document.Records.Select(r => (r.Id, RowText(r, name), (Texture2D)null)));
            entityList.Select(selected?.Id);
        }

        private string RowText(EntityRecord record, LocaleFieldEditor nameField)
        {
            var key = nameField == null ? null : record.Data[nameField.Field.Key]?.ToString();
            var language = languages.FirstOrDefault() ?? "ru";
            var text = key == null ? null : document.GetText(language, key) ?? LocaleFileService.Read(modId, language, key);
            return string.IsNullOrWhiteSpace(text) ? record.Id : $"{text}  ·  {record.Id}";
        }

        // ── form ───────────────────────────────────────────────────

        private void BuildForm()
        {
            foreach (var child in form.GetChildren())
            {
                form.RemoveChild(child);
                child.QueueFree();
            }
            editors.Clear();
            localeEditors.Clear();
            fileLabel = ToolUi.Hint(string.Empty);
            form.AddChild(fileLabel);
            idEdit = ToolUi.Field(form, "tools/entity_editor/id", ToolUi.Text(string.Empty, OnTyped), "tools/entity_editor/id_tip");
            idEdit.Name = "Id";
            idEdit.AddThemeFontOverride("font", ToolUi.Mono());
            foreach (var field in kind.Fields.Where(f => !string.Equals(f.Key, "Id", StringComparison.OrdinalIgnoreCase)))
                editors.Add(CreateEditor(field));
            foreach (var locale in localeEditors)
                locale.Open(document, modId, languages);

            form.AddChild(ToolUi.Section("tools/entity_editor/section_other"));
            form.AddChild(ToolUi.Hint(ToolUi.Tr("tools/entity_editor/other_hint")));
            otherEdit = ToolUi.Code();
            otherEdit.Name = "Other";
            otherEdit.CustomMinimumSize = new Vector2(0, 140);
            otherEdit.TextChanged += OnOtherChanged;
            form.AddChild(otherEdit);
        }

        private ISchemaFieldEditor CreateEditor(FieldSchema field)
        {
            var editor = SchemaFieldEditors.Create(form, field, WriteForm);
            if (editor is LocaleFieldEditor locale)
                localeEditors.Add(locale);
            return editor;
        }

        private void ShowSelected()
        {
            emptyHint.Visible = selected == null;
            form.Visible = selected != null;
            if (selected == null)
                return;
            suppress = true;
            fileLabel.Text = string.Format(ToolUi.Tr("tools/entity_editor/file"), modId, selected.File);
            idEdit.Text = selected.Id;
            foreach (var editor in editors)
                editor.Load(Find(selected.Data, editor.Field.Key));
            otherEdit.Text = OtherJson(selected.Data);
            suppress = false;
            editing = false;
        }

        private static JsonNode Find(JsonObject data, string key)
            => data.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

        private void OnTyped(string text) => WriteForm();

        private void WriteForm()
        {
            if (suppress || selected == null)
                return;
            if (!editing)
            {
                document.BeginChange();
                editing = true;
            }
            var data = selected.Data;
            data["Id"] = idEdit.Text.Trim();
            foreach (var editor in editors)
            {
                var existing = data.FirstOrDefault(p => string.Equals(p.Key, editor.Field.Key, StringComparison.OrdinalIgnoreCase)).Key ?? editor.Field.Key;
                var value = editor.Save();
                if (value == null)
                    data.Remove(existing);
                else
                    data[existing] = value;
            }
            SetDirty();
            RefreshList();
        }

        private bool IsFormKey(string key)
            => key == "Id" || kind.Fields.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

        private string OtherJson(JsonObject data)
        {
            var other = new JsonObject();
            foreach (var pair in data.Where(p => !IsFormKey(p.Key)))
                other[pair.Key] = pair.Value?.DeepClone();
            return EntityDocument.Serialize(other).TrimEnd();
        }

        private void OnOtherChanged()
        {
            if (suppress || selected == null)
                return;
            JsonObject other;
            try
            {
                other = JsonNode.Parse(otherEdit.Text) as JsonObject;
            }
            catch (JsonException ex)
            {
                SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/other_invalid"), ex.Message), ToolStatusKind.Error);
                return;
            }
            if (other == null)
                return;
            if (!editing)
            {
                document.BeginChange();
                editing = true;
            }
            var data = selected.Data;
            foreach (var key in data.Select(p => p.Key).Where(k => !IsFormKey(k)).ToList())
                data.Remove(key);
            foreach (var pair in other.Where(p => !IsFormKey(p.Key)).ToList())
                data[pair.Key] = pair.Value?.DeepClone();
            SetDirty();
        }

        // ── commands ───────────────────────────────────────────────

        /// <summary>Adds a new entity of the kind with the schema's defaults (Ctrl+N).</summary>
        public void AddNew()
        {
            if (document == null || kind == null)
                return;
            document.BeginChange();
            var pattern = string.IsNullOrWhiteSpace(kind.NewId) ? "{mod}/new_" + kind.Kind.Replace('/', '_') : kind.NewId;
            var id = document.FreeId(pattern.Replace("{mod}", modId));
            var entity = new JsonObject { ["Id"] = id };
            foreach (var field in kind.Fields.Where(f => !string.IsNullOrEmpty(f.Default)))
                entity[field.Key] = SchemaJson.ToNode(field, field.Default);
            foreach (var field in kind.Fields.Where(f => f.Type == "locale" && !string.IsNullOrEmpty(f.KeyPattern)))
            {
                var key = field.KeyPattern.Replace("{mod}", modId).Replace("{id}", id).Replace("{key}", field.Key.ToLowerInvariant());
                entity[field.Key] = key;
                foreach (var language in languages)
                    document.SetText(language, key, field.Key == "Name" ? id.Substring(id.LastIndexOf('/') + 1) : string.Empty);
            }
            var record = document.Add(entity);
            SetDirty();
            selected = record;
            RefreshList();
            Select(record.Id);
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/added"), id, record.File));
        }

        private void DuplicateSelected()
        {
            if (selected == null)
                return;
            document.BeginChange();
            var record = document.Duplicate(selected, document.FreeId(selected.Id));
            SetDirty();
            selected = record;
            RefreshList();
            Select(record.Id);
        }

        private void DeleteSelected()
        {
            if (selected == null)
                return;
            ShowConfirm("tools/entity_editor/delete_title", "tools/entity_editor/delete_text", ConfirmDelete);
        }

        private void ConfirmDelete()
        {
            if (selected == null)
                return;
            document.BeginChange();
            document.Remove(selected);
            selected = null;
            SetDirty();
            RefreshList();
            ShowSelected();
        }

        private void Undo() => StepHistory(document != null && document.Undo());

        private void Redo() => StepHistory(document != null && document.Redo());

        private void StepHistory(bool stepped)
        {
            if (!stepped)
                return;
            var id = selected?.Id;
            SetDirty();
            RefreshList();
            Select(document.Find(id)?.Id ?? document.Records.FirstOrDefault()?.Id);
        }

        /// <inheritdoc />
        protected override void OnSaveShortcut() => Save();

        private void OnSavePressed() => Save();

        /// <summary>
        /// Writes the changed data and locale files and makes the game re-read them (entity
        /// and file caches, current language, the tools' own entity index).
        /// </summary>
        public bool Save()
        {
            if (document == null)
                return false;
            var problems = document.Validate();
            if (problems.Count > 0)
            {
                SetStatus(string.Format(ToolUi.Tr(problems[0].Key), problems[0].Argument), ToolStatusKind.Error);
                return false;
            }
            var changedFiles = document.ChangedFiles();
            var changedTexts = document.ChangedTexts();
            foreach (var (path, text) in changedFiles)
            {
                if (!ModFileService.WriteText(modId, path, text))
                {
                    SetStatus(ToolUi.Tr("tools/common/readonly"), ToolStatusKind.Error);
                    return false;
                }
            }
            foreach (var (language, key, value) in changedTexts)
                LocaleFileService.Write(modId, language, key, value);
            document.MarkSaved();
            MarkClean();
            editing = false;
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            GameEntityIndex.Refresh();
            if (changedTexts.Count > 0)
                LocaleFileService.ReloadGame();
            GameLogger.Log("TOOLS", $"data editor: saved {changedFiles.Count} {kind.Kind} file(s), {changedTexts.Count} text(s) of '{modId}'");
            SetStatus(string.Format(ToolUi.Tr("tools/entity_editor/saved"), changedFiles.Count, changedTexts.Count, modId), ToolStatusKind.Ok);
            FillKinds();
            kindList.Select(kind.Kind);
            return true;
        }

        /// <inheritdoc />
        protected override bool HandleKey(InputEventKey key)
        {
            if (!key.CtrlPressed)
                return false;
            switch (key.Keycode)
            {
                case Key.Z when key.ShiftPressed:
                case Key.Y:
                    Redo();
                    return true;
                case Key.Z:
                    Undo();
                    return true;
                case Key.N:
                    AddNew();
                    return true;
                case Key.D:
                    DuplicateSelected();
                    return true;
            }
            return false;
        }
    }
}
