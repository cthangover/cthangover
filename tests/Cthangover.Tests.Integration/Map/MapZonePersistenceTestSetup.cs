#if TOOLS
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration test for world map zone persistence. Exercises the real save
    /// pipeline (<see cref="SaveService"/>) with the <see cref="MapZoneService"/>
    /// singleton to prove that runtime open/hidden overrides survive a full
    /// save → new state → load cycle through the core <c>__core__</c> slot, and
    /// that zone IDs unknown to the loaded data do not break restoration.
    /// Zones are injected directly (no mod map files are required), so the test
    /// is independent of the map content that happens to ship with the mods.
    /// </summary>
    public partial class MapZonePersistenceTestSetup : Node
    {
        private const string SaveName = "map_zones_test";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            if (!ModManager.Instance.IsInitialized)
                ModManager.Instance.Initialize();

            var service = MapZoneService.Instance;
            service.LoadZones(new List<MapZone>
            {
                new MapZone { ID = "map_test_plain" },
                new MapZone { ID = "map_test_locked", Locked = true },
                new MapZone { ID = "map_test_hidden", Hidden = true },
            });

            service.Open("map_test_locked");
            service.Hide("map_test_plain");

            AssertTrue(service.IsOpen("map_test_locked"), "opened zone is open before save");
            AssertFalse(service.IsVisible("map_test_plain"), "hidden zone is hidden before save");

            SaveService.Save(SaveName);
            Pass("save written");

            service.ResetOverrides();
            AssertFalse(service.IsOpen("map_test_locked"), "reset drops the open override");
            AssertTrue(service.IsVisible("map_test_plain"), "reset drops the hidden override");

            var loaded = SaveService.Load(SaveName);
            AssertTrue(loaded, "SaveService.Load returns true");
            AssertTrue(service.IsOpen("map_test_locked"), "open override restored from the core slot");
            AssertFalse(service.IsVisible("map_test_plain"), "hidden override restored from the core slot");
            AssertTrue(service.IsOpen("map_test_hidden"), "untouched zone keeps its open authoring default");
            AssertFalse(service.IsVisible("map_test_hidden"), "hidden authoring default survives the load");

            SaveService.DeleteSave(SaveName);
            Finish();
        }

        private void AssertTrue(bool condition, string label)
        {
            if (condition)
                Pass(label);
            else
                Fail($"expected true: {label}");
        }

        private void AssertFalse(bool condition, string label)
        {
            if (!condition)
                Pass(label);
            else
                Fail($"expected false: {label}");
        }

        private void Pass(string message)
        {
            _passed++;
            GameLogger.Log("MAP_TEST", $"  [PASS] {message}");
        }

        private void Fail(string message)
        {
            _failed++;
            GameLogger.Log("MAP_TEST", $"  [FAIL] {message}", LogLevel.Error);
        }

        private void Finish()
        {
            GameLogger.Log("MAP_TEST", $"MapZonePersistenceTestSetup: {_passed} passed, {_failed} failed");
            GameLogger.Log("MAP_TEST", $"[TEST_RESULT] name=map_zones passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }
    }
}
#endif
