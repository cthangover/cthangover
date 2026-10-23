using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Characters;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.Tool;
using Cthangover.Mods.Rpg;
using Cthangover.Tools.RpgEditor;
using Cthangover.Tools.Services;
using Godot;

namespace Cthangover.Tools.CharactersEditor.Tests
{
    /// <summary>
    /// Drives the character editor like an author: the toolbar buttons and the
    /// form's fields (their change signals), Save. Then the game itself is asked:
    /// the entity cache must hold the new character with the typed health and its
    /// action, the translation server its typed name, and the RPG map editor's
    /// character list must offer it with the typed charset. The new characters
    /// file is deleted and the mod's locale files are restored afterwards, so
    /// rpg_demo_game stays as it was.
    /// Result line: <c>[TEST_RESULT] name=characters_editor_edit passed=N failed=M</c>.
    /// </summary>
    public partial class CharacterEditorTestDriver : Node
    {
        private const float TickSeconds = 0.15f;
        private const float TimeoutSeconds = 60f;
        private const string Mod = "rpg_demo_game";
        private const string NewFile = "characters/rpg_demo_game.json";
        private const string NewId = "rpg_demo_game_new_character";
        private const string TypedName = "Тестовый гоблин";

        private readonly List<string> failures = new();
        private readonly Dictionary<string, string> backups = new();
        private readonly Dictionary<string, string> characterFiles = new();
        private int passed;
        private int step;
        private double elapsed;
        private CharacterEditorWindow window;

        /// <summary>Creates the driver; <paramref name="shotsDir"/> is unused (no screenshots).</summary>
        public CharacterEditorTestDriver(string shotsDir)
        {
            Name = "CharacterEditorTestDriver";
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
                    foreach (var file in ModFileService.ListFiles(Mod, "characters", ".json"))
                        characterFiles[file] = ModFileService.ReadText(Mod, file);
                    Check(!characterFiles.ContainsKey(NewFile), "the mod has no editor characters file yet");
                    window = ToolWindow.Open<CharacterEditorWindow>();
                    step++;
                    return;
                case 1:
                    if (window.Document == null)
                        return;
                    window.OpenMod(Mod);
                    var language = Language();
                    var slime = window.Document.Find("rpg_slime");
                    Check(slime != null, "the demo's characters are listed");
                    window.Select(slime);
                    Check(window.Document.GetText(language, "rpg_demo/character/slime") == TranslationServer.Translate("rpg_demo/character/slime").ToString(),
                        $"the form shows the character's name in '{language}'");

                    Press("tools/entity_editor/new");
                    Check(window.SelectedId == NewId, $"New adds and selects {NewId} (got {window.SelectedId})");
                    Type(NameEdit(), TypedName);
                    Row("tools/characters_editor/health").OfType<SpinBox>().First().Value = 33;
                    Type(Row("tools/characters_editor/charset").OfType<LineEdit>().First(), "village/slime");
                    var record = window.Document.Find(NewId);
                    Check(record.Data["Health"]?.ToString() == "33" && record.Data["Charset"]?.ToString() == "village/slime", "the form writes health and the charset");
                    Check(record.Data["Actions"]?.AsArray().Any(a => a?.ToString() == "physics/attack") == true, "a new character starts with the plain attack");
                    step++;
                    return;
                case 2:
                    try
                    {
                        Press("tools/common/save");
                        var characters = Entities.Collect<CharacterInfo>();
                        Check(characters.TryGetValue(NewId, out var saved), "the game knows the new character right after saving");
                        Check(saved?.Health == 33 && saved.Actions?.Contains("physics/attack") == true, "with the typed health and its action");
                        Check(TranslationServer.Translate(saved?.Name ?? "-").ToString() == TypedName, $"and the typed name ({saved?.Name})");
                        var offered = RpgCharacterCatalog.Load(new RpgTextures()).FirstOrDefault(e => e.Id == NewId);
                        Check(offered?.Look == "village/slime" && offered.Name == TypedName, $"the RPG map editor offers it with its charset ({offered?.Look})");
                        var written = ModFileService.ReadText(Mod, NewFile) ?? string.Empty;
                        Check(written.Contains("\"Kind\": \"character\"") && !written.Contains("\r"), "the new file declares its kind and has LF lines");
                        Check(characterFiles.All(f => ModFileService.ReadText(Mod, f.Key) == f.Value), "the demo's character files are untouched");
                    }
                    finally
                    {
                        Cleanup();
                    }
                    Finish();
                    return;
            }
        }

        /// <summary>The controls of the form row labelled <paramref name="labelKey"/>.</summary>
        private IEnumerable<Node> Row(string labelKey)
        {
            var text = TranslationServer.Translate(labelKey).ToString();
            return window.FindChildren("*", "", true, false).OfType<Label>().First(l => l.Text == text).GetParent().FindChildren("*", "", true, false);
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
            GameLogger.Log("TEST", $"[TEST_RESULT] name=characters_editor_edit passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
