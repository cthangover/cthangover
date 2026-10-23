using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Items;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Tools.Services;
using Godot;

namespace Cthangover.Tools.ItemsEditor.Tests
{
    /// <summary>
    /// Drives the item editor like an author: the toolbar buttons, the form's
    /// fields (their change signals), Ctrl+Z into the window, Save. Then the game
    /// itself is asked: the entity cache must hold the new item with the typed
    /// price and type, and the translation server its typed name. The new items
    /// file is deleted and the mod's locale files are restored afterwards, so
    /// rpg_demo_game stays as it was.
    /// Result line: <c>[TEST_RESULT] name=items_editor_edit passed=N failed=M</c>.
    /// </summary>
    public partial class ItemEditorTestDriver : Node
    {
        private const float TickSeconds = 0.15f;
        private const float TimeoutSeconds = 60f;
        private const string Mod = "rpg_demo_game";
        private const string NewFile = "items/rpg_demo_game.json";
        private const string NewId = "rpg_demo_game/new_item";
        private const string TypedName = "Тестовый амулет";

        private readonly List<string> failures = new();
        private readonly Dictionary<string, string> backups = new();
        private readonly Dictionary<string, string> itemFiles = new();
        private int passed;
        private int step;
        private double elapsed;
        private ItemEditorWindow window;

        /// <summary>Creates the driver; <paramref name="shotsDir"/> is unused (no screenshots).</summary>
        public ItemEditorTestDriver(string shotsDir)
        {
            Name = "ItemEditorTestDriver";
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private void Tick()
        {
            if (step < 0)
                return;
            elapsed += TickSeconds;
            if (elapsed > TimeoutSeconds)
            {
                Fail($"timeout at step {step}");
                Finish();
                return;
            }
            try
            {
                RunStep();
            }
            catch (System.Exception ex)
            {
                Fail($"step {step}: {ex.GetType().Name}: {ex.Message}");
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
                    foreach (var file in ModFileService.ListFiles(Mod, "locale", ".properties"))
                        backups[file] = ModFileService.ReadText(Mod, file);
                    foreach (var file in ModFileService.ListFiles(Mod, "items", ".json"))
                        itemFiles[file] = ModFileService.ReadText(Mod, file);
                    Check(!itemFiles.ContainsKey(NewFile), "the mod has no editor items file yet");
                    window = ToolWindow.Open<ItemEditorWindow>();
                    step++;
                    return;
                case 1:
                    if (window.Document == null)
                        return;
                    window.OpenMod(Mod);
                    var language = Language();
                    var branches = window.Document.Find("rpg_demo/branches");
                    Check(branches != null, "the demo's items are listed");
                    window.Select(branches);
                    Check(window.Document.GetText(language, "rpg_demo/item/branches/name") == TranslationServer.Translate("rpg_demo/item/branches/name").ToString(),
                        $"the form shows the item's name in '{language}'");

                    Press("tools/entity_editor/new");
                    Check(window.SelectedId == NewId, $"New adds and selects {NewId} (got {window.SelectedId})");
                    Type(NameEdit(), TypedName);
                    var cost = window.FindChildren("*", "", true, false).OfType<SpinBox>().First();
                    cost.Value = 7;
                    var food = window.FindChildren("*", "", true, false).OfType<CheckBox>().First(c => c.Text == nameof(ItemType.Food));
                    food.ButtonPressed = true;
                    var record = window.Document.Find(NewId);
                    Check(record.Data["Cost"]?.ToString() == "7" && record.Data["ItemType"]?.ToString().Split(',').OrderBy(t => t).SequenceEqual(new[] { "Food", "Resource" }) == true, "the form writes price and types");

                    Press("tools/entity_editor/duplicate");
                    Check(window.Document.Find(NewId + "_2") != null, "Copy adds a copy under a free id");
                    window.PushInput(new InputEventKey { Keycode = Key.Z, PhysicalKeycode = Key.Z, CtrlPressed = true, Pressed = true });
                    Check(window.Document.Find(NewId + "_2") == null && window.Document.Find(NewId) != null, "Ctrl+Z takes the copy back");
                    step++;
                    return;
                case 2:
                    try
                    {
                        Press("tools/common/save");
                        var items = Entities.Collect<ItemInfo>();
                        Check(items.TryGetValue(NewId, out var saved), "the game knows the new item right after saving");
                        Check(saved?.Cost == 7 && saved.ItemType.HasFlag(ItemType.Food) && saved.ItemType.HasFlag(ItemType.Resource), "with the typed price and types");
                        Check(TranslationServer.Translate(saved?.Name ?? "-").ToString() == TypedName, $"and the typed name ({saved?.Name})");
                        var written = ModFileService.ReadText(Mod, NewFile) ?? string.Empty;
                        Check(written.Contains("\"Kind\": \"item\"") && !written.Contains("\r") && written.Contains(TypedName) == false, "the new file declares its kind, has LF lines and no texts");
                        Check(itemFiles.All(f => ModFileService.ReadText(Mod, f.Key) == f.Value), "the demo's item files are untouched");
                    }
                    finally
                    {
                        Cleanup();
                    }
                    Finish();
                    return;
            }
        }

        private static string Language() => LanguageCode.Normalize(TranslationServer.GetLocale()) ?? "ru";

        /// <summary>The first text row of the current language: the name (the first locale field).</summary>
        private LineEdit NameEdit()
        {
            var label = string.Format(TranslationServer.Translate("tools/entity_editor/text_in"), Language());
            return window.FindChildren("*", "", true, false).OfType<Label>().First(l => l.Text == label && l.IsVisibleInTree())
                .GetParent().GetChildren().OfType<LineEdit>().First();
        }

        private static void Type(LineEdit edit, string text)
        {
            edit.Text = text;
            edit.EmitSignal(LineEdit.SignalName.TextChanged, text);
        }

        private void Cleanup()
        {
            window?.QueueFree();
            var path = ModFileService.GetWritablePath(Mod, NewFile);
            if (path != null && File.Exists(path))
                File.Delete(path);
            foreach (var backup in backups)
                ModFileService.WriteText(Mod, backup.Key, backup.Value);
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            LocaleFileService.ReloadGame();
        }

        private void Press(string key)
        {
            var text = TranslationServer.Translate(key);
            var button = window.FindChildren("*", "", true, false).OfType<Button>().FirstOrDefault(b => b.Text == text);
            if (button == null)
            {
                Fail($"button '{text}' not found");
                return;
            }
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void Check(bool condition, string what)
        {
            if (condition)
            {
                passed++;
                GameLogger.Log("TEST", $"[PASS] {what}");
            }
            else
            {
                Fail(what);
            }
        }

        private void Fail(string what)
        {
            failures.Add(what);
            GameLogger.Log("TEST", $"[FAIL] {what}", LogLevel.Error);
        }

        private void Finish()
        {
            step = -1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=items_editor_edit passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
