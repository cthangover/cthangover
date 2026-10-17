using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.SaveDormancyTest
{
    /// <summary>
    /// Runs the composition-change round trip once the game is running and
    /// prints <c>[TEST_RESULT] name=save_dormancy</c>: a save of the active
    /// game lands in its own folder; data of a mod the game runs without
    /// survives a load and the next save as a dormant slot; a mod without data
    /// in the save gets <c>OnJoinedExistingGame</c>; the purge drops dormant data.
    /// </summary>
    public partial class SaveDormancyTestDriverNode : Node
    {
        private const string SlotA = "itest_dormancy_a";
        private const string SlotB = "itest_dormancy_b";
        private const string AbsentMod = "recruiting";
        private const float TimeoutSeconds = 90f;

        private readonly List<string> failures = new();
        private int passed;
        private double elapsed;
        private bool done;

        public override void _Process(double delta)
        {
            if (done)
                return;

            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                failures.Add("timeout waiting for the game");
                Finish();
                return;
            }

            if (!Boot.IsReady || GameData.Instance?.Runtime == null || GetTree().CurrentScene == null || elapsed < 3)
                return;

            try
            {
                Run();
            }
            catch (Exception ex)
            {
                failures.Add($"unhandled {ex.GetType().Name}: {ex.Message}");
            }

            Finish();
        }

        private void Run()
        {
            Check("a game collection is active", ActiveGame.IsActive);
            Check($"'{AbsentMod}' is not part of the game", !ActiveGame.ModIds.Contains(AbsentMod));

            SaveService.Save(SlotA);
            var pathA = FilePath(SlotA);
            Check("save lands in the game's folder", pathA.Contains($"saves{Path.DirectorySeparatorChar}{ActiveGame.Id}", StringComparison.Ordinal) || pathA.Contains($"saves/{ActiveGame.Id}", StringComparison.Ordinal));
            var package = Read(pathA);
            Check("save records the game id", package?.Game?.Id == ActiveGame.Id);
            Check("save records the composition", package?.Game?.Mods?.Count > 0);

            // Simulate a save written while the game still had the mechanic, and
            // before this test mod joined the game.
            var absentSlot = new ModSlot { Metadata = new ModMetadata { ModId = AbsentMod, ModVersion = "1.1.0" } };
            absentSlot.Data["marker"] = new DataBlob(1, new Dictionary<string, string> { ["value"] = "42" });
            package.ModData[AbsentMod] = absentSlot;
            package.ModData.Remove("base_menu__save_dormancy");
            File.WriteAllText(pathA, JsonSerializer.Serialize(package, JsonOptionsProvider.WriteCompactOptions));

            SaveDormancyProbe.Joined = false;
            SaveDormancyProbe.Restored = false;
            Check("load succeeds despite a missing mod", SaveService.Load(SlotA));
            Check("missing mod's data went dormant", SaveService.LastLoadDormancy?.Dormant.ContainsKey(AbsentMod) == true);
            Check("mod without data joined the game", SaveDormancyProbe.Joined && !SaveDormancyProbe.Restored);

            SaveService.Save(SlotB);
            var resaved = Read(FilePath(SlotB));
            var carried = resaved?.Dormant != null && resaved.Dormant.TryGetValue(AbsentMod, out var slot) ? slot : null;
            Check("dormant slot carried into the next save", carried?.Metadata?.Dormant == true);
            Check("dormant data unchanged", carried?.Data != null && carried.Data.TryGetValue("marker", out var marker)
                                            && marker.ReadJson<Dictionary<string, string>>().GetValueOrDefault("value") == "42");
            Check("live mods still saved", resaved?.ModData?.ContainsKey(SavePackage.CoreSlotKey) == true);

            var message = ModSettingsRegistry.RunAction("base_menu", "purge_dormant");
            Check("purge action runs from the save settings", !string.IsNullOrEmpty(message));
            Check("purge removed dormant data from the save", Read(FilePath(SlotB))?.Dormant == null);

            SaveService.DeleteSave(SlotA);
            SaveService.DeleteSave(SlotB);
        }

        private static string FilePath(string slot)
            => ProjectSettings.GlobalizePath($"{SaveService.SaveDirectory}/{slot}.json");

        private static SavePackage Read(string path)
            => File.Exists(path) ? JsonSerializer.Deserialize<SavePackage>(File.ReadAllText(path), JsonOptionsProvider.ReadOptions) : null;

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"SaveDormancyTest: [OK] {name}");
            }
            else
            {
                failures.Add(name);
                GameLogger.Log("TEST", $"SaveDormancyTest: [FAIL] {name}", LogLevel.Error);
            }
        }

        private void Finish()
        {
            if (done)
                return;
            done = true;
            foreach (var failure in failures)
                GameLogger.Log("TEST", $"SaveDormancyTest: failed: {failure}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=save_dormancy passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
