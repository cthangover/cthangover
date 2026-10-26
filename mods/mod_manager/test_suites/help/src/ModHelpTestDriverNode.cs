using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.Menu;
using Cthangover.Mods.Interface.Theming;
using Cthangover.Core.UI.Theming;
using Cthangover.Mods.Interface.View;
using Godot;
using Mods.ModManager;

namespace Mods.ModManagerHelpTest
{
    using System.Text.Json;
    using Cthangover.Core.Mods;
    /// <summary>
    /// Exercises contextual help as a player uses it: pin a hint beside a mod search,
    /// keep the hub interactive while help is open, inspect a mod page and the scheme
    /// legend, invoke F1 from keyboard focus and leave with Escape without navigating
    /// away. Also rejects tooltips that merely repeat their button captions and
    /// exercises Graphite's scoped version edits, duplicate occurrences, zoom and
    /// overview, shared dialog chrome and title-bar close hit areas. Collection
    /// edits remain drafts and the user's layout is restored.
    /// <c>--shots=&lt;dir&gt;</c> saves the inspected states. Prints mod_help's result.
    /// </summary>
    public partial class ModHelpTestDriverNode : Node
    {
        private const string HubId = "mods_hub";
        private const string GameId = "novel_template";
        private const float TimeoutSeconds = 90f;

        private readonly List<string> failures = new();
        private int passed;
        private int step;
        private double elapsed;
        private double stepAt;
        private bool done;
        private ModHubScreen hub;
        private ModCollectionsScreen collectionEditor;
        private CollectionSchemeCanvas scheme;
        private Vector2 firstOccurrencePosition;
        private string layoutPath;
        private byte[] layoutBackup;

        public override void _Process(double delta)
        {
            if (done)
                return;

            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                failures.Add($"timeout at step {step}");
                Finish();
                return;
            }

            if (elapsed - stepAt < 0.5)
                return;
            stepAt = elapsed;

            try
            {
                RunStep();
            }
            catch (Exception ex)
            {
                failures.Add($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady)
                        return;
                    if (GetTree().CurrentScene?.FindChild("LauncherPlay", true, false) is Button launcherPlay)
                    {
                        launcherPlay.EmitSignal(BaseButton.SignalName.Pressed);
                        return;
                    }
                    if (GetTree().CurrentScene?.FindChild("play", true, false) is not Button)
                        return;
                    var entry = MenuRegistry.GetButtonById(HubId);
                    hub = MenuBuilder.OpenScene((Control)GetTree().CurrentScene, entry?.ScenePath, HubId) as ModHubScreen;
                    Check("the hub opens", hub != null);
                    if (hub == null)
                        Finish();
                    break;

                case 1:
                {
                    PressTab("mod_manager/hub_tab_mods");
                    CheckLevel("mods");
                    PressHelp();
                    ModHelpPanel help = Help();
                    Check("the header enables help mode", help != null && help.Visible);
                    Check("help has no dim backdrop", help != null && help.GetNodeOrNull("Dim") == null);
                    Check("help does not capture the mouse", help != null && help.MouseFilter == Control.MouseFilterEnum.Ignore);
                    Control search = Annotated("mod_search");
                    CheckHelp("mods_search", search);
                    break;
                }

                case 2:
                {
                    ModHelpPanel help = Help();
                    Check("a shown hint is pinned", help != null && help.IsPinned);
                    var card = hub.FindChildren("*", "", true, false).OfType<ModCard>().FirstOrDefault(c => c.IsVisibleInTree());
                    Check("the grid shows mod cards", card != null);
                    card?.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
                    break;
                }

                case 3:
                {
                    Check("the hub stays interactive while help is open", hub.FindChildren("*", "", true, false).OfType<ModDetailView>().Any(d => d.IsVisibleInTree()));
                    CheckLevel("mod_page");
                    CheckHelp("mod_version", Annotated("mod_version"));
                    PressTab("mod_manager/hub_tab_collections");
                    break;
                }

                case 4:
                {
                    Check("collections open without closing help", Gallery() != null && Gallery().IsVisibleInTree() && Help().Visible);
                    CheckLevel("gallery");
                    EscapeHelp();
                    hub.OpenCollectionEditor(GameId);
                    break;
                }

                case 5:
                {
                    CheckLevel("scheme");
                    Control legend = Annotated("load_order");
                    Check("the scheme always shows its load-order legend", legend != null && legend.IsVisibleInTree());
                    CheckHelp("scheme_load_order", legend);
                    var group = hub.FindChildren("*", "", true, false).OfType<CollectionGroupCard>().FirstOrDefault(g => g.IsVisibleInTree());
                    Check("the scheme shows mechanics", group != null);
                    Click(group);
                    break;
                }

                case 6:
                {
                    CheckLevel("scheme_mechanic");
                    var chip = hub.FindChildren("*", "", true, false).OfType<CollectionModChip>().FirstOrDefault(c => c.IsVisibleInTree());
                    Check("the mechanic shows its mods", chip != null);
                    Click(chip);
                    break;
                }

                case 7:
                {
                    CheckLevel("scheme_mod");
                    Control links = Annotated("collection_links");
                    Check("the links toggle has keyboard focus", links != null);
                    links?.GrabFocus();
                    hub._UnhandledInput(new InputEventKey { Keycode = Key.F1, Pressed = true });
                    ModHelpPanel help = Help();
                    Check("F1 explains the focused control", help != null && help.CurrentTarget == links && help.IsPinned);
                    Shot("scheme_f1");
                    break;
                }

                case 8:
                    // Leave the F1 state visible for the deferred screenshot.
                    EscapeHelp();
                    Check("Escape leaves help without closing the editor", Annotated("load_order") != null);
                    hub.OpenCollectionEditor("cthangover");
                    break;

                case 9:
                    collectionEditor = FindControl<ModCollectionsScreen>(hub);
                    scheme = FindControl<CollectionSchemeCanvas>(collectionEditor);
                    Check("graphite has the existing native canvas", scheme != null);
                    Check("graphite has a scene-defined version form", collectionEditor?.FindChild("VersionEditor", true, false) != null);
                    Check("graphite has a visible overview", FindControl<CollectionMiniMap>(collectionEditor)?.Size.X > 0);
                    CheckSourceSyntax();
                    if (GameEnvironment.IsRuntime)
                    {
                        layoutPath = ProjectSettings.GlobalizePath("user://collection_layout.json");
                        layoutBackup = File.Exists(layoutPath) ? File.ReadAllBytes(layoutPath) : null;
                    }
                    Shot("graphite_01_collection");
                    break;

                case 10:
                    SetGraphiteFixture();
                    break;

                case 11:
                    var second = FindOccurrence("m:cthangover:second");
                    firstOccurrencePosition = FindOccurrence("m:cthangover:first").Position;
                    Click(second);
                    Check("shared mod selection retains its clicked group", scheme.SelectedGroupKey == "m:cthangover:second");
                    Check("version form reads the selected occurrence's pin", VersionField<LineEdit>("Value").Text == "1");
                    Shot("graphite_02_shared_context");
                    break;

                case 12:
                    SelectVersionPolicy("=");
                    VersionField<LineEdit>("Value").Text = "999.0.0";
                    PressEditorButton("SaveRule");
                    Shot("graphite_03_version_conflict");
                    break;

                case 13:
                    Check("incompatible version blocks apply", collectionEditor.FindChild("Apply", true, false) is Button apply && apply.Disabled);
                    PressEditorButton("Json");
                    Shot("graphite_06_json_dialog");
                    break;

                case 14:
                    var jsonDialog = FindControl<ConfirmationDialog>(collectionEditor);
                    CheckDialogChrome("JSON", jsonDialog);
                    var jsonText = FindControl<TextEdit>(jsonDialog);
                    Check("collection JSON uses a highlighted code editor", jsonText is CodeEdit && jsonText.SyntaxHighlighter != null);
                    var edited = JsonSerializer.Deserialize<ModCollection>(jsonText.Text);
                    Check("version edit preserves the other group", edited.Mechanics[0].Mods[0] == "ui_base>=0");
                    Check("version edit survives the real JSON editor", edited.Mechanics[1].Mods[0] == "ui_base=999.0.0");
                    jsonDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
                    break;

                case 15:
                    SelectVersionPolicy(">=");
                    VersionField<LineEdit>("Value").Text = "1";
                    PressEditorButton("SaveRule");
                    PressEditorButton("TabProblems");
                    Check("restoring a compatible pin clears problems", HasLabel(collectionEditor, TranslationServer.Translate("mod_manager/collection_problems_none")));
                    PressEditorButton("TabInspector");
                    var zoom = scheme.Zoom;
                    PressEditorButton("ZoomOut");
                    Check("zoom updates the view scale", scheme.Zoom < zoom);
                    Check("zoom preserves logical chip positions", FindOccurrence("m:cthangover:first").Position == firstOccurrencePosition);
                    break;

                case 16:
                    var movedChip = FindOccurrence("m:cthangover:second");
                    var beforeMove = movedChip.Position;
                    MoveOccurrence(movedChip, new Vector2(12f, 4f));
                    var stored = CollectionLayoutStore.Load("cthangover");
                    var position = stored.Mods[movedChip.GroupKey + "|" + movedChip.ModId];
                    Check("drag persists the selected occurrence", Mathf.IsEqualApprox(position.X, beforeMove.X + 12f));
                    Check("drag leaves the first occurrence unchanged", FindOccurrence("m:cthangover:first").Position == firstOccurrencePosition);
                    scheme.CenterOn(new Vector2(2000f, 1500f));
                    var beforePan = scheme.VisibleBounds.Position;
                    var map = FindControl<CollectionMiniMap>(collectionEditor);
                    map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
                    {
                        ButtonIndex = MouseButton.Left, Pressed = true, Position = map.Size / 2f,
                    });
                    Check("overview click moves the scroll viewport", scheme.VisibleBounds.Position != beforePan);
                    break;

                case 17:
                    PressEditorButton("Fit");
                    Shot("graphite_04_layout");
                    break;

                case 18:
                    SetIncludedFixture();
                    break;

                case 19:
                    Click(FindOccurrence("i:cthangover:dev_tools>=1", "editor_tools"));
                    SelectVersionPolicy("=");
                    VersionField<LineEdit>("Value").Text = "not a version?";
                    PressEditorButton("SaveRule");
                    Check("invalid input is explained", VersionField<Label>("Feedback").Text == TranslationServer.Translate("mod_manager/collection_version_invalid"));
                    PressEditorButton("Json");
                    break;

                case 20:
                    var invalidDialog = FindControl<ConfirmationDialog>(collectionEditor);
                    var unchanged = JsonSerializer.Deserialize<ModCollection>(FindControl<TextEdit>(invalidDialog).Text);
                    Check("invalid input does not fork or repoint an included block", unchanged.Includes[0].Ref == "dev_tools>=1");
                    invalidDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
                    break;

                case 21:
                    SelectVersionPolicy("=");
                    VersionField<LineEdit>("Value").Text = ModRegistry.Instance.AllDiscoveredMods["editor_tools"].Version;
                    PressEditorButton("SaveRule");
                    Check("valid edit retains selection in the per-game block copy", scheme.SelectedGroupKey == "i:cthangover:dev_tools.cthangover");
                    PressEditorButton("Json");
                    break;

                case 22:
                    var copiedDialog = FindControl<ConfirmationDialog>(collectionEditor);
                    var copied = JsonSerializer.Deserialize<ModCollection>(FindControl<TextEdit>(copiedDialog).Text);
                    Check("valid edit repoints only this game's include", copied.Includes[0].Ref == "dev_tools.cthangover");
                    copiedDialog.EmitSignal(ConfirmationDialog.SignalName.Canceled);
                    break;

                case 23:
                    Shot("graphite_05_included_version");
                    break;

                case 24:
                    SetGraphiteFixture();
                    break;

                case 25:
                    var renameCard = FindControl<CollectionGroupCard>(collectionEditor);
                    Click(renameCard);
                    break;

                case 26:
                    var rename = Buttons(collectionEditor).OfType<Button>().First(b => b.IsVisibleInTree() && b.Text == TranslationServer.Translate("mod_manager/collection_rename"));
                    rename.EmitSignal(BaseButton.SignalName.Pressed);
                    Shot("graphite_07_rename_dialog");
                    break;

                case 27:
                    var renameDialog = FindControl<AcceptDialog>(collectionEditor);
                    CheckDialogChrome("rename", renameDialog);
                    ClickDialogClose(renameDialog);
                    break;

                case 28:
                    Check("title-bar close dismisses rename", FindControl<AcceptDialog>(collectionEditor) == null);
                    PressEditorButton("Json");
                    break;

                case 29:
                    ClickDialogClose(FindControl<ConfirmationDialog>(collectionEditor));
                    break;

                case 30:
                    Check("title-bar close dismisses JSON", FindControl<ConfirmationDialog>(collectionEditor) == null);
                    Finish();
                    break;
            }

            step++;
        }

        private void CheckSourceSyntax()
        {
            var editor = new TextEdit { Visible = false };
            collectionEditor.AddChild(editor);
            editor.SyntaxHighlighter = SourceSyntax.CreateJsonHighlighter();
            editor.Text = "{\"name\": \"value # ; 123\", \"enabled\": true, \"count\": -2.5, \"none\": null}";
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf("value", StringComparison.Ordinal), UiTokens.AccentBright, "JSON string");
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf('#'), UiTokens.AccentBright, "JSON comment marker inside a string");
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf("true", StringComparison.Ordinal), UiTokens.TypeTarget, "JSON boolean");
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf("2.5", StringComparison.Ordinal), UiTokens.Info, "JSON number");
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf("null", StringComparison.Ordinal), UiTokens.TypeTarget, "JSON null");
            editor.Text = "{\"value\": \"a\\\"#b\", \"enabled\": false}";
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf('#'), UiTokens.AccentBright, "escaped JSON quote");
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf("false", StringComparison.Ordinal), UiTokens.TypeTarget, "JSON literal after an escaped string");
            editor.Text = "{\"unfinished\": \"text";
            CheckSyntaxColor(editor, 0, editor.Text.IndexOf("text", StringComparison.Ordinal), UiTokens.AccentBright, "unfinished JSON after editing");

            editor.SyntaxHighlighter = SourceSyntax.CreateTscnHighlighter();
            editor.Text = "[gd_scene load_steps=2 format=3]\n[node name=\"Control\" type=\"Control\"]\nposition = Vector2(12, -4)\n; comment 7";
            CheckSyntaxColor(editor, 0, 1, UiTokens.TypeTarget, "TSCN section");
            CheckSyntaxColor(editor, 1, 12, UiTokens.AccentBright, "TSCN string");
            CheckSyntaxColor(editor, 2, 11, UiTokens.TypeTarget, "TSCN constructor");
            CheckSyntaxColor(editor, 2, 19, UiTokens.Info, "TSCN number");
            CheckSyntaxColor(editor, 3, 2, UiTokens.Faint, "TSCN comment");

            editor.SyntaxHighlighter = SourceSyntax.CreateScenarioHighlighter(new[] { "mod_command" });
            editor.Text = "mod_command target=\"res://#path\" wait=2\n# comment\n:next\noption \"Continue\" -> :next";
            CheckSyntaxColor(editor, 0, 0, UiTokens.Info, "registered scenario command");
            CheckSyntaxColor(editor, 0, 23, UiTokens.AccentBright, "scenario path inside a string");
            CheckSyntaxColor(editor, 0, 33, UiTokens.Accent, "scenario named argument");
            CheckSyntaxColor(editor, 1, 2, UiTokens.Faint, "scenario comment");
            CheckSyntaxColor(editor, 2, 1, UiTokens.TypeTarget, "scenario label");
            CheckSyntaxColor(editor, 3, 0, UiTokens.Info, "scenario option");
            Check("highlighting preserves source text", editor.Text == "mod_command target=\"res://#path\" wait=2\n# comment\n:next\noption \"Continue\" -> :next");
            editor.Free();
        }

        private void CheckSyntaxColor(TextEdit editor, int line, int column, Color expected, string token)
        {
            var spans = editor.SyntaxHighlighter.GetLineSyntaxHighlighting(line);
            var nearest = -1;
            var actual = Colors.Transparent;
            foreach (var key in spans.Keys)
            {
                var start = key.AsInt32();
                if (start > column || start < nearest)
                    continue;
                nearest = start;
                actual = spans[key].AsGodotDictionary()["color"].AsColor();
            }
            Check(token + " is highlighted", actual.IsEqualApprox(expected));
        }

        private void CheckDialogChrome(string name, AcceptDialog dialog)
        {
            var icon = dialog.GetThemeIcon("close", "Window");
            var top = dialog.GetThemeConstant("close_v_offset", "Window");
            var right = dialog.GetThemeConstant("close_h_offset", "Window");
            Check(name + ": close icon fits entirely inside the title bar",
                top >= icon.GetHeight() && top <= dialog.GetThemeConstant("title_height", "Window")
                && right >= icon.GetWidth());
            var frame = (StyleBoxFlat)dialog.GetThemeStylebox("embedded_border", "Window");
            Check(name + ": rounded outer corners clear the opaque content viewport",
                frame.ExpandMarginTop >= dialog.GetThemeConstant("title_height", "Window")
                && frame.ExpandMarginLeft >= frame.CornerRadiusBottomLeft
                && frame.ExpandMarginRight >= frame.CornerRadiusBottomRight
                && frame.ExpandMarginBottom >= Math.Max(frame.CornerRadiusBottomLeft, frame.CornerRadiusBottomRight));
        }

        private void ClickDialogClose(AcceptDialog dialog)
        {
            // Headless has no rendered chrome to hit-test. Deliver the same
            // window-manager request there; the GPU run clicks the real icon.
            if (DisplayServer.GetName() == "headless")
            {
                dialog.Notification((int)NotificationWMCloseRequest);
                return;
            }
            var icon = dialog.GetThemeIcon("close", "Window");
            var point = (Vector2)dialog.Position + new Vector2(
                dialog.Size.X - dialog.GetThemeConstant("close_h_offset", "Window"),
                -dialog.GetThemeConstant("close_v_offset", "Window")) + icon.GetSize() / 2f;
            GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = true });
            GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = false });
        }

        private void SetGraphiteFixture()
        {
            var fixture = new ModCollection
            {
                Id = "cthangover", Name = "Graphite selection check",
                Mechanics = new List<ModCollectionMechanic>
                {
                    new() { Id = "first", Name = "First role", Role = "engine", Mods = new List<string> { "ui_base>=0" } },
                    new() { Id = "second", Name = "Second role", Role = "extra", Mods = new List<string> { "ui_base>=1" } },
                },
            };
            PressEditorButton("Json");
            var dialog = FindControl<ConfirmationDialog>(collectionEditor);
            FindControl<TextEdit>(dialog).Text = JsonSerializer.Serialize(fixture);
            dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        }

        private void SetIncludedFixture()
        {
            var fixture = new ModCollection
            {
                Id = "cthangover", Name = "Graphite included version check",
                Includes = new List<ModCollectionInclude> { new() { Ref = "dev_tools>=1", Role = "tools" } },
            };
            PressEditorButton("Json");
            var dialog = FindControl<ConfirmationDialog>(collectionEditor);
            FindControl<TextEdit>(dialog).Text = JsonSerializer.Serialize(fixture);
            dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        }

        private CollectionModChip FindOccurrence(string group, string modId = "ui_base")
        {
            foreach (var child in scheme.FindChildren("*", "", true, false))
            {
                if (child is CollectionModChip chip && chip.GroupKey == group && chip.ModId == modId)
                    return chip;
            }
            throw new InvalidOperationException("Mod occurrence is absent: " + group);
        }

        private T VersionField<T>(string name) where T : Node
            => collectionEditor.FindChild("VersionEditor", true, false)?.FindChild(name, true, false) as T;

        private static T FindControl<T>(Node root) where T : Node
        {
            if (root == null)
                return null;
            foreach (var child in root.FindChildren("*", "", true, false))
            {
                if (child is T match)
                    return match;
            }
            return null;
        }

        private static bool HasLabel(Node root, string text)
        {
            foreach (var child in root.FindChildren("*", "", true, false))
            {
                if (child is Label label && label.Text.Contains(text, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void PressEditorButton(string name)
        {
            var button = collectionEditor.FindChild(name, true, false) as Button;
            if (button == null)
                throw new InvalidOperationException("Editor button is absent: " + name);
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void SelectVersionPolicy(string op)
        {
            var policy = VersionField<OptionButton>("Policy");
            for (var i = 0; i < policy.ItemCount; i++)
            {
                if (policy.GetItemMetadata(i).AsString() != op)
                    continue;
                policy.Selected = i;
                policy.EmitSignal(OptionButton.SignalName.ItemSelected, (long)i);
                return;
            }
            throw new InvalidOperationException("Version policy is absent: " + op);
        }

        private static void MoveOccurrence(CollectionModChip chip, Vector2 delta)
        {
            var panel = FindControl<PanelContainer>(chip);
            panel.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            panel.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Relative = delta, ButtonMask = MouseButtonMask.Left });
            panel.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        }

        /// <summary>A left click on the frame of a scheme mechanic or mod chip.</summary>
        private static void Click(Control owner)
        {
            var panel = owner?.GetChildren().OfType<PanelContainer>().FirstOrDefault();
            panel?.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            panel?.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        }

        private void PressTab(string key)
        {
            var text = TranslationServer.Translate(key).ToString();
            var tab = Buttons(hub).FirstOrDefault(b => TextOf(b) == text && b.IsVisibleInTree() && b.ToggleMode);
            tab ??= Buttons(hub).FirstOrDefault(b => TextOf(b) == text && b.IsVisibleInTree());
            Check($"tab '{text}' shown", tab != null);
            tab?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void PressHelp()
        {
            var help = Buttons(hub).FirstOrDefault(b => b.Name == "HelpButton" && b.IsVisibleInTree());
            Check("the header offers contextual help", help != null);
            help?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void EscapeHelp()
        {
            hub._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
            Check("Escape closes help", Help() != null && !Help().Visible);
        }

        private ModHelpPanel Help()
            => hub.FindChildren("*", "", true, false).OfType<ModHelpPanel>().FirstOrDefault();

        private CollectionsGallery Gallery()
            => hub.FindChildren("*", "", true, false).OfType<CollectionsGallery>().FirstOrDefault();

        private Control Annotated(string key)
            => hub.FindChildren("*", "", true, false).OfType<Control>().FirstOrDefault(c
                => c.IsVisibleInTree()
                   && c.HasMeta(ModHelpPanel.HintKeyMetadata)
                   && c.GetMeta(ModHelpPanel.HintKeyMetadata).AsString() == key);

        private void CheckHelp(string level, Control target)
        {
            Check($"{level}: an annotated target exists", target != null);
            ModHelpPanel help = Help();
            if (help == null || target == null)
                return;

            help.Open();
            help.ShowFor(target);
            Check($"{level}: the target is explained", help.CurrentTarget == target && help.IsPinned);

            var card = help.GetNode<PanelContainer>("Card");
            var body = help.GetNode<RichTextLabel>("Card/Margin/Column/Body");
            var title = help.GetNode<Label>("Card/Margin/Column/Title");
            Check($"{level}: localized title and body are shown",
                card.Visible
                && title.Text.Length > 0
                && body.GetParsedText().Length > 20
                && !title.Text.Contains("mod_manager/hint_")
                && !body.Text.Contains("mod_manager/hint_"));

            Rect2 cardRect = card.GetGlobalRect();
            Rect2 viewport = help.GetViewportRect();
            Check($"{level}: the card stays inside the viewport",
                cardRect.Position.X >= viewport.Position.X - 1f
                && cardRect.Position.Y >= viewport.Position.Y - 1f
                && cardRect.End.X <= viewport.End.X + 1f
                && cardRect.End.Y <= viewport.End.Y + 1f);
            Check($"{level}: the card does not cover its target", !cardRect.Intersects(target.GetGlobalRect()));
            Shot(level);
        }

        /// <summary>Tooltips must add information, not repeat visible captions or sit on mouse-ignoring controls.</summary>
        private void CheckLevel(string level)
        {
            Shot(level);
            var echoes = new List<string>();
            var deaf = new List<string>();
            foreach (var control in hub.FindChildren("*", "", true, false).OfType<Control>())
            {
                if (!control.IsVisibleInTree())
                    continue;
                var tip = control.TooltipText;
                if (control is Button button
                    && !string.IsNullOrWhiteSpace(button.Text)
                    && string.Equals(button.Text.Trim(), tip?.Trim(), StringComparison.Ordinal))
                {
                    echoes.Add(Describe(control));
                }
                if (!string.IsNullOrWhiteSpace(tip) && control.MouseFilter == Control.MouseFilterEnum.Ignore)
                    deaf.Add(Describe(control));
            }

            Check($"{level}: no tooltip repeats its button" + (echoes.Count > 0 ? ": " + string.Join(", ", echoes) : string.Empty), echoes.Count == 0);
            Check($"{level}: no tooltip ignores the mouse" + (deaf.Count > 0 ? ": " + string.Join(", ", deaf) : string.Empty), deaf.Count == 0);
        }

        private static string Describe(Control control)
        {
            var text = control switch
            {
                Button button => button.Text,
                LineEdit edit => edit.PlaceholderText,
                _ => string.Empty,
            };
            return $"{control.GetClass()} '{control.Name}'" + (string.IsNullOrEmpty(text) ? string.Empty : $" \"{text}\"");
        }

        private static IEnumerable<BaseButton> Buttons(Node root)
            => root == null ? Enumerable.Empty<BaseButton>() : root.FindChildren("*", "", true, false).OfType<BaseButton>();

        private static string TextOf(BaseButton button)
            => button is Button clickable ? clickable.Text : string.Empty;

        /// <summary>With <c>--shots=&lt;dir&gt;</c> saves the settled window as <paramref name="name"/>.png.</summary>
        private async void Shot(string name)
        {
            string dir = null;
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--shots="))
                    dir = arg.Substring("--shots=".Length);
            }
            if (string.IsNullOrEmpty(dir) || DisplayServer.GetName() == "headless")
                return;

            // ViewportCapture reads the last rendered frame. Wait until the new
            // target, highlight and card have actually been presented.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (done && name != "scheme_f1")
                return;
            dir = ProjectSettings.GlobalizePath(dir);
            Directory.CreateDirectory(dir);
            ViewportCapture.SavePng(GetViewport(), Path.Combine(dir, name + ".png"));
        }

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"ModHelpTest: [OK] {name}");
            }
            else
            {
                failures.Add(name);
                GameLogger.Log("TEST", $"ModHelpTest: [FAIL] {name}", LogLevel.Error);
            }
        }

        private async void Finish()
        {
            if (done)
                return;
            done = true;
            hub?.QueueFree();
            if (layoutPath != null)
            {
                if (layoutBackup == null)
                    File.Delete(layoutPath);
                else
                    File.WriteAllBytes(layoutPath, layoutBackup);
            }
            // Let queued scene teardown reach the renderer before terminating
            // the native window; otherwise GPU checks report live viewport RIDs.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GameLogger.Log("TEST", $"[TEST_RESULT] name=mod_help passed={passed} failed={failures.Count}");
            foreach (var failure in failures)
                GameLogger.Log("TEST", $"ModHelpTest: failure: {failure}", LogLevel.Error);
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
