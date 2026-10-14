#if TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration test for world map zone persistence. Exercises the real save
    /// pipeline (<see cref="SaveService"/>) with the map_base mod's
    /// MapZoneService singleton to prove that runtime open/hidden overrides
    /// survive a full save → new state → load cycle through the core
    /// <c>__core__</c> slot, and that zone IDs unknown to the loaded data do
    /// not break restoration. Zones are injected directly (no mod map files
    /// are required), so the test is independent of the map content that
    /// happens to ship with the mods.
    /// <para/>
    /// The zone service lives in the map_base engine mod, so every access to
    /// its types (MapZoneService, MapZone) goes through reflection here — the
    /// same rule every core-side setup follows.
    /// </summary>
    public partial class MapZonePersistenceTestSetup : Node
    {
        private const string SaveName = "map_zones_test";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.

            var serviceType = ModType("MapZoneService");
            var service = serviceType?.GetProperty("Instance")?.GetValue(null);
            var zoneType = ModType("MapZone");
            if (service == null || zoneType == null)
            {
                Fail("map_base mod types are not loaded");
                Finish();
                return;
            }

            var zones = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(zoneType));
            zones.Add(MakeZone(zoneType, "map_test_plain", locked: false, hidden: false));
            zones.Add(MakeZone(zoneType, "map_test_locked", locked: true, hidden: false));
            zones.Add(MakeZone(zoneType, "map_test_hidden", locked: false, hidden: true));

            InvokeOn(service, "LoadZones", zones);
            InvokeOn(service, "Open", "map_test_locked");
            InvokeOn(service, "Hide", "map_test_plain");

            AssertTrue(IsOpen(service), "opened zone is open before save");
            AssertFalse(IsVisible(service, "map_test_plain"), "hidden zone is hidden before save");

            SaveService.Save(SaveName);
            Pass("save written");

            InvokeOn(service, "ResetOverrides");
            AssertFalse(IsOpen(service), "reset drops the open override");
            AssertTrue(IsVisible(service, "map_test_plain"), "reset drops the hidden override");

            var loaded = SaveService.Load(SaveName);
            AssertTrue(loaded, "SaveService.Load returns true");
            AssertTrue(IsOpen(service), "open override restored from the core slot");
            AssertFalse(IsVisible(service, "map_test_plain"), "hidden override restored from the core slot");
            AssertTrue(IsOpen(service, "map_test_hidden"), "untouched zone keeps its open authoring default");
            AssertFalse(IsVisible(service, "map_test_hidden"), "hidden authoring default survives the load");

            SaveService.DeleteSave(SaveName);
            Finish();
        }

        // ---- reflection helpers over the map_base mod assembly --------------

        /// <summary>Resolves a mod type by simple name in the Cthangover.Mods.Map namespace.</summary>
        private static Type ModType(string simpleName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => a.GetType("Cthangover.Mods.Map." + simpleName))
                .FirstOrDefault(t => t != null);
        }

        private static object MakeZone(Type zoneType, string id, bool locked, bool hidden)
        {
            var zone = Activator.CreateInstance(zoneType);
            zoneType.GetProperty("ID")?.SetValue(zone, id);
            zoneType.GetProperty("Locked")?.SetValue(zone, locked);
            zoneType.GetProperty("Hidden")?.SetValue(zone, hidden);
            return zone;
        }

        private static object InvokeOn(object instance, string method, params object[] args)
        {
            return instance.GetType().GetMethod(method)?.Invoke(instance, args);
        }

        private bool IsOpen(object service, string id = "map_test_locked")
        {
            return Equals(InvokeOn(service, "IsOpen", id), true);
        }

        private bool IsVisible(object service, string id)
        {
            return Equals(InvokeOn(service, "IsVisible", id), true);
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
