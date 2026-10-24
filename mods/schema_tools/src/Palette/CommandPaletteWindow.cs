namespace Cthangover.Tools.Schemas
{
    using Cthangover.Mods.Schemas;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Core.UI.Tool.ScenarioEditor;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Tools.Common;
    using Godot;

    /// <summary>
    /// The command palette: every scenario action and command some mod described, searchable
    /// and grouped, with a form built from its schema. The author fills the form (pickers
    /// for entities, check boxes, formula fields), sees the line it makes and what the
    /// validator would say about it, and inserts the line into the open scenario editor
    /// below the caret — or copies it. Nothing about an action is hard-coded here: a new
    /// mod with a schema file appears in the palette as soon as it is loaded.
    /// <para/>
    /// The text stays the source of truth: the palette writes exactly the DSL line an author
    /// would have typed, so a professional keeps typing and a beginner never has to.
    /// </summary>
    public partial class CommandPaletteWindow : ToolWindow
    {
        private const string ActionPrefix = "action:";
        private const string CommandPrefix = "command:";

        private readonly List<SchemaFieldControl> controls = new();
        private FilteredList list;
        private VBoxContainer form;
        private Label titleLabel;
        private Label helpLabel;
        private LineEdit arrowEdit;
        private LineEdit preview;
        private Label issuesLabel;
        private ActionSchema action;
        private CommandSchema command;

        /// <summary>Builds the window over the current schema catalog.</summary>
        public CommandPaletteWindow() : base("tools/schema/palette/title")
        {
            CthangoverTheme.Apply(this);
            BuildUi();
            Fill();
            SetHint(ToolUi.Tr("tools/schema/palette/hint"));
        }

        /// <summary>The line the form makes now (empty before anything is chosen).</summary>
        public string Line => preview?.Text ?? string.Empty;

        /// <summary>Chooses an entry: <c>action:&lt;id&gt;</c> or <c>command:&lt;word&gt;</c>.</summary>
        public void Choose(string entryId)
        {
            list.Select(entryId);
            OnChosen(entryId);
        }

        /// <summary>Sets a form value by parameter key (tests drive the form like a user).</summary>
        public void SetValue(string key, string value)
        {
            var control = controls.FirstOrDefault(c => string.Equals(c.Field.Key, key, StringComparison.OrdinalIgnoreCase));
            if (control == null)
                return;
            control.Value = value;
            Rebuild();
        }

        /// <summary>Inserts <see cref="Line"/> below the caret of the open scenario editor; false when none is open.</summary>
        public bool InsertIntoEditor()
        {
            var tabs = GetTree()?.Root.FindChildren("*", "", true, false).OfType<ScenarioEditTabs>().FirstOrDefault(t => t.HasActiveFile);
            if (tabs == null || string.IsNullOrEmpty(Line))
            {
                SetStatus(ToolUi.Tr("tools/schema/palette/no_editor"), ToolStatusKind.Error);
                return false;
            }
            var code = tabs.CodeEditor;
            var at = Math.Min(code.GetCaretLine() + 1, code.GetLineCount());
            code.InsertLineAt(at, Line);
            code.SetCaretLine(at);
            SetStatus(string.Format(ToolUi.Tr("tools/schema/palette/inserted"), tabs.CurrentKey), ToolStatusKind.Ok);
            return true;
        }

        private void BuildUi()
        {
            var outer = CreateFillContainer();
            AddChild(outer);
            var toolbar = CreateToolbar();
            toolbar.AddChild(ToolUi.Button("tools/schema/palette/insert", OnInsertPressed, ButtonKind.Primary, "tools/schema/palette/insert_tip"));
            toolbar.AddChild(ToolUi.Button("tools/schema/palette/copy", OnCopyPressed, ButtonKind.Secondary, "tools/schema/palette/copy_tip"));
            outer.AddChild(toolbar);

            var main = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            outer.AddChild(main);
            var left = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            left.AddChild(ToolUi.Section("tools/schema/palette/section_list"));
            list = new FilteredList(360f, "tools/schema/palette/search") { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            list.Chosen += OnChosen;
            left.AddChild(list);
            main.AddChild(CreateSidebar(left, 340));

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            main.AddChild(scroll);
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", UiTokens.Space2);
            scroll.AddChild(column);
            titleLabel = UiKit.Label(ToolUi.Tr("tools/schema/palette/pick"), UiTokens.FontBodyLarge, UiTokens.InkBright);
            column.AddChild(titleLabel);
            helpLabel = ToolUi.Hint(string.Empty);
            column.AddChild(helpLabel);
            form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            form.AddThemeConstantOverride("separation", UiTokens.Space2);
            column.AddChild(form);
            column.AddChild(ToolUi.Section("tools/schema/palette/section_line"));
            preview = new LineEdit { Editable = false, Name = "Preview", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            preview.AddThemeFontOverride("font", ToolUi.Mono());
            column.AddChild(preview);
            issuesLabel = ToolUi.Hint(string.Empty);
            issuesLabel.Name = "Issues";
            column.AddChild(issuesLabel);
        }

        private void Fill()
        {
            var catalog = SchemaCatalog.Current;
            var items = new List<(string Id, string Text, Texture2D Icon)>();
            foreach (var a in catalog.Actions.Values.OrderBy(a => a.Category).ThenBy(a => a.Id, StringComparer.Ordinal))
                items.Add((ActionPrefix + a.Id, Entry(a.Category, a.Name, "action " + a.Id), null));
            foreach (var c in catalog.Commands.Values.OrderBy(c => c.Category).ThenBy(c => c.Id, StringComparer.Ordinal))
                items.Add((CommandPrefix + c.Id, Entry(c.Category, c.Name, c.Id), null));
            list.SetItems(items);
            SetStatus(string.Format(ToolUi.Tr("tools/schema/palette/count"), catalog.Actions.Count, catalog.Commands.Count));
        }

        private static string Entry(string category, string nameKey, string syntax)
        {
            var name = string.IsNullOrEmpty(nameKey) ? syntax : ToolUi.Tr(nameKey);
            var group = string.IsNullOrEmpty(category) ? string.Empty : $"[{Category(category)}] ";
            return $"{group}{name}  ·  {syntax}";
        }

        private static string Category(string category)
        {
            var key = "tools/schema/category/" + category;
            var text = ToolUi.Tr(key);
            return text == key ? category : text;
        }

        private void OnChosen(string entryId)
        {
            action = null;
            command = null;
            var catalog = SchemaCatalog.Current;
            if (entryId?.StartsWith(ActionPrefix, StringComparison.Ordinal) == true)
                catalog.Actions.TryGetValue(entryId.Substring(ActionPrefix.Length), out action);
            else if (entryId?.StartsWith(CommandPrefix, StringComparison.Ordinal) == true)
                catalog.Commands.TryGetValue(entryId.Substring(CommandPrefix.Length), out command);
            BuildForm();
        }

        private void BuildForm()
        {
            foreach (var child in form.GetChildren())
            {
                form.RemoveChild(child);
                child.QueueFree();
            }
            controls.Clear();
            arrowEdit = null;
            var nameKey = action?.Name ?? command?.Name;
            titleLabel.Text = nameKey == null ? ToolUi.Tr("tools/schema/palette/pick") : ToolUi.Tr(nameKey);
            var help = action?.Description ?? command?.Description;
            var source = action?.SourceMod ?? command?.SourceMod;
            helpLabel.Text = (help == null ? string.Empty : ToolUi.Tr(help) + "\n") + (source == null ? string.Empty : string.Format(ToolUi.Tr("tools/schema/described_by"), source));
            var fields = action != null ? action.Params : command != null ? (command.Positional ?? new()).Concat(command.Params ?? new()).ToList() : new List<FieldSchema>();
            foreach (var field in fields)
                controls.Add(new SchemaFieldControl(form, field, Rebuild));
            if (command?.Arrow != null)
                arrowEdit = ToolUi.Field(form, command.Arrow, ToolUi.Text(":label", OnArrowTyped), "tools/schema/palette/arrow_tip");
            Rebuild();
        }

        private void OnArrowTyped(string text) => Rebuild();

        private void Rebuild()
        {
            var values = controls.ToDictionary(c => c.Field.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);
            List<SchemaIssue> issues;
            if (action != null)
            {
                preview.Text = SchemaLine.Action(action, values);
                issues = SchemaChecker.CheckNamed(action.Id, action.Params, values.Where(p => p.Value.Length > 0).ToDictionary(p => p.Key, p => p.Value), GameEntityIndex.Exists);
            }
            else if (command != null)
            {
                preview.Text = SchemaLine.Command(command, values, arrowEdit?.Text);
                var positional = (command.Positional ?? new()).Select(f => values.GetValueOrDefault(f.Key)).ToList();
                issues = SchemaChecker.CheckPositional(command.Id, command.Positional, positional, GameEntityIndex.Exists);
                issues.AddRange(SchemaChecker.CheckNamed(command.Id, command.Params, (command.Params ?? new()).Where(f => values.GetValueOrDefault(f.Key)?.Length > 0).ToDictionary(f => f.Key, f => values[f.Key]), GameEntityIndex.Exists));
            }
            else
            {
                preview.Text = string.Empty;
                issues = new List<SchemaIssue>();
            }
            issuesLabel.Text = issues.Count == 0 ? (preview.Text.Length > 0 ? ToolUi.Tr("tools/schema/palette/line_ok") : string.Empty) : string.Join("\n", issues.Select(i => (i.IsError ? "✖ " : "⚠ ") + i.Message));
            issuesLabel.AddThemeColorOverride("font_color", issues.Any(i => i.IsError) ? UiTokens.DangerText : issues.Count > 0 ? UiTokens.Warning : UiTokens.SuccessText);
        }

        private void OnInsertPressed() => InsertIntoEditor();

        private void OnCopyPressed()
        {
            if (string.IsNullOrEmpty(Line))
                return;
            DisplayServer.ClipboardSet(Line);
            SetStatus(ToolUi.Tr("tools/schema/palette/copied"), ToolStatusKind.Ok);
        }
    }
}
