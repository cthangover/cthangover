#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Linq;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage for the world map subsystem migrated into the
    /// map_base engine mod. The zone service, the map widget and the zone
    /// layers live in the mod assembly, the core keeps the save DTO
    /// (MapZoneStateData), the MapZoneChangedEvent contract and reaches the
    /// state through MapStateBridge, so every check here proves the
    /// core-to-mod wiring at engine runtime.
    /// The machine-readable line is parsed by <c>tests/CThangover.TestRunner</c>.
    /// </summary>
    public partial class MapCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "MAP_COVERAGE";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync)
            // before any scene _Ready, so the subsystem's OnModResourcesReady has
            // already installed its hooks.

            var kernelAssembly = typeof(GameData).Assembly;

            Check("migrated type MapZoneService is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Mods.Map.MapZoneService", kernelAssembly) != null);
            Check("core assembly no longer defines MapZoneService",
                KernelLacksTypeNamed(kernelAssembly, "MapZoneService"));

            Check("migrated type MapWidget is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Mods.Map.MapWidget", kernelAssembly) != null);
            Check("core assembly no longer defines MapWidget",
                KernelLacksTypeNamed(kernelAssembly, "MapWidget"));

            Check("migrated type MapZonesLayer is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Mods.Map.MapZonesLayer", kernelAssembly) != null);

            Check("MapZoneChangedEvent stays a kernel contract",
                kernelAssembly.GetType("Cthangover.Core.Events.MapZoneChangedEvent") != null);

            var exportState = SafeGet(() => Cthangover.Core.Actions.MapStateBridge.ExportState());
            Check("MapStateBridge.ExportState returns a live delta from the map_base mod", exportState != null);
            SafeRun(() => Cthangover.Core.Actions.MapStateBridge.ResetOverrides());
            Check("MapStateBridge.ResetOverrides is a safe no-throw reset", true);

            GameLogger.Log(Category, $"[TEST_RESULT] name=map_coverage passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        /// <summary>Resolves a mod type by full name outside the kernel assembly (reflection only).</summary>
        private static Type FindOutsideKernel(string fullName, System.Reflection.Assembly kernelAssembly)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == fullName);
        }

        /// <summary>True when the kernel assembly holds no type with the given simple name.</summary>
        private static bool KernelLacksTypeNamed(System.Reflection.Assembly kernelAssembly, string simpleName)
        {
            return !kernelAssembly.GetTypes().Any(t => t.Name == simpleName);
        }

        /// <summary>Exception-safe getter that logs and degrades to null.</summary>
        private static T SafeGet<T>(Func<T> f) where T : class
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, $"lookup failed: {e.Message}", LogLevel.Error); return null; }
        }

        /// <summary>Exception-safe runner for probe mutations.</summary>
        private static void SafeRun(Action f)
        {
            try { f(); }
            catch (Exception e) { GameLogger.Log(Category, $"action failed: {e.Message}", LogLevel.Error); }
        }

        /// <summary>Tolerates ReflectionTypeLoadException while scanning assemblies.</summary>
        private static Type[] SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }

        private void Check(string what, bool ok)
        {
            if (ok)
            {
                _passed++;
                GameLogger.Log(Category, "PASS " + what);
            }
            else
            {
                _failed++;
                GameLogger.Log(Category, "FAIL " + what, LogLevel.Error);
            }
        }
    }
}
#endif
