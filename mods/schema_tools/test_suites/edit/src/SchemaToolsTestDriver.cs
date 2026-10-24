namespace Cthangover.Tools.Schemas.Tests
{
    using Cthangover.Mods.Schemas;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.UI.Tool.ScenarioEditor;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Equipment;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Stats;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>
    /// Drives the Data Editor window and the command palette like an author: a new item gets
    /// gear fields that two different mods described, the game sees it as gear right after
    /// Save, and the palette writes and checks a stats line. Everything the test writes is
    /// removed again, the files it changed are restored byte for byte.
    /// </summary>
    public partial class SchemaToolsTestDriver : Node
    {
        private const float TickSeconds = 0.15f;
        private const float TimeoutSeconds = 60f;
        private const string Mod = "rpg_plus_game";
        private const string NewFile = "items/rpg_plus_game.json";
        private const string NewId = "rpg_plus_game/new_item";
        private const string ScenarioKey = "rpg_plus_game:scenarios/rpg_plus/events/seer.scenario";

        private readonly List<string> failures = new();
        private readonly Dictionary<string, string> backups = new();
        private int passed;
        private int step;
        private double elapsed;
        private bool newFileExisted;
        private bool itemsDirExisted;
        private DataEditorWindow editor;
        private CommandPaletteWindow palette;
        private ScenarioEditorWindow scenarios;

        public SchemaToolsTestDriver()
        {
            Name = "SchemaToolsTestDriver";
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
                Cleanup();
                Finish();
                return;
            }
            try
            {
                RunStep();
            }
            catch (System.Exception ex)
            {
                Fail($"step {step}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                Cleanup();
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
                    newFileExisted = ModFileService.ReadText(Mod, NewFile) != null;
                    itemsDirExisted = Directory.Exists(Path.GetDirectoryName(ModFileService.GetWritablePath(Mod, NewFile)) ?? string.Empty);
                    Check(!newFileExisted, "the game mod has no items file of its own yet");
                    editor = ToolWindow.Open<DataEditorWindow>();
                    step++;
                    return;
                case 1:
                    if (!editor.IsInsideTree())
                        return;
                    var kinds = SchemaCatalog.Current.Kinds;
                    Check(new[] { "stat", "archetype", "equip_slot", "item", "character", "action", "shop" }.All(kinds.ContainsKey), "the editor lists the kinds of five mods' schemas");
                    editor.OpenKind("item");
                    editor.OpenMod(Mod);
                    Check(editor.KindId == "item" && editor.ModId == Mod, "items of the game mod are open");
                    editor.AddNew();
                    Check(editor.SelectedId == NewId, $"New adds {NewId} (got {editor.SelectedId})");
                    Type(editor, "Text_Equip", "sword");
                    var list = editor.FindChild("List_Traits", true, false);
                    Check(list != null, "the equipment mod's Traits field is a list editor in the item form");
                    (list?.FindChild("Add", true, false) as Button)?.EmitSignal(BaseButton.SignalName.Pressed);
                    step++;
                    return;
                case 2:
                    var card = editor.FindChild("List_Traits", true, false)?.FindChild("Card0", true, false);
                    Check(card != null, "Add made a trait card");
                    var picker = card?.FindChild("Picker_Stat", true, false) as OptionButton;
                    var index = Enumerable.Range(0, picker?.ItemCount ?? 0).FirstOrDefault(i => picker.GetItemText(i).EndsWith(" atk") || picker.GetItemText(i) == "atk");
                    Check(picker != null && index > 0, "the trait's stat picker offers the pack's stats");
                    picker?.Select(index);
                    picker?.EmitSignal(OptionButton.SignalName.ItemSelected, index);
                    Type(card, "Text_Add", "3");
                    var data = editor.Document.Find(NewId)?.Data;
                    Check(data?["Equip"]?.ToString() == "sword", "the form wrote the equip type");
                    Check(data?["Traits"]?[0]?["Stat"]?.ToString() == "atk" && data?["Traits"]?[0]?["Add"]?.ToString() == "3", $"the form wrote the trait: {data?["Traits"]?.ToJsonString()}");
                    Check(editor.Save(), "Save writes the mod");
                    Check(EquipmentService.IsEquipment(NewId), "the game knows the new item as gear right after saving");
                    var traits = ExtraData.Traits(EquipmentService.Item(NewId)?.ExtraProperties, NewId);
                    Check(traits.Count == 1 && traits[0].Stat == "atk" && traits[0].Add == 3, "with its trait");
                    var written = ModFileService.ReadText(Mod, NewFile) ?? string.Empty;
                    Check(written.Contains("\"Kind\": \"item\"") && !written.Contains("\r"), "the new file declares its kind and has LF lines");
                    palette = ToolWindow.Open<CommandPaletteWindow>();
                    scenarios = ToolWindow.Open<ScenarioEditorWindow>();
                    step++;
                    return;
                case 3:
                    if (!palette.IsInsideTree() || !scenarios.IsInsideTree())
                        return;
                    scenarios.OpenScenario(ScenarioKey);
                    palette.Choose("action:stat.add");
                    palette.SetValue("stat", "str");
                    palette.SetValue("value", "2");
                    Check(palette.Line == "action stat.add stat=str value=2", $"the palette writes the line ({palette.Line})");
                    Check((palette.FindChild("Issues", true, false) as Label)?.Text.StartsWith("✔") == true, "and finds nothing wrong with it");
                    palette.SetValue("stat", "strenght");
                    Check((palette.FindChild("Issues", true, false) as Label)?.Text.Contains("✖") == true, "a misspelt stat is reported as an error");
                    palette.SetValue("stat", "str");
                    step++;
                    return;
                case 4:
                    var tabs = scenarios.FindChildren("*", "", true, false).OfType<ScenarioEditTabs>().FirstOrDefault();
                    if (tabs == null || !tabs.HasActiveFile)
                    {
                        if (elapsed > 30)
                            Fail("the scenario editor did not open the seer's scenario");
                        else
                            return;
                    }
                    Check(palette.InsertIntoEditor(), "Insert puts the line into the open scenario");
                    Check(tabs?.CurrentText.Contains("action stat.add stat=str value=2") == true, "the editor's text has the line");
                    Check(ModFileService.ReadText(Mod, "scenarios/rpg_plus/events/seer.scenario")?.Contains("stat.add") == false, "nothing is written to disk before the author saves");
                    Cleanup();
                    Finish();
                    return;
            }
        }

        private static void Type(Node root, string name, string text)
        {
            if (root?.FindChild(name, true, false) is not LineEdit edit)
                return;
            edit.Text = text;
            edit.EmitSignal(LineEdit.SignalName.TextChanged, text);
        }

        private void Cleanup()
        {
            if (step < 0)
                return;
            foreach (var window in new Node[] { editor, palette, scenarios }.Where(w => w != null && IsInstanceValid(w)))
                window.QueueFree();
            var path = ModFileService.GetWritablePath(Mod, NewFile);
            if (!newFileExisted && path != null && File.Exists(path))
                File.Delete(path);
            // The save created the folder too; only that empty folder of this run goes away.
            var folder = path == null ? null : Path.GetDirectoryName(path);
            if (!itemsDirExisted && folder != null && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
            foreach (var backup in backups)
                ModFileService.WriteText(Mod, backup.Key, backup.Value);
            ModManager.Instance.GetResolver<EntityResolver>()?.ClearCache();
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            GameEntityIndex.Refresh();
            LocaleFileService.ReloadGame();
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
            GameLogger.Log("TEST", $"[TEST_RESULT] name=schema_tools_edit passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
