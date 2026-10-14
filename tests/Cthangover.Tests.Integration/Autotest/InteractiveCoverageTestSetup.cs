#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Linq;
    using Cthangover.Core.Interactive;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage for the interactive subsystem migrated into the
    /// interactive_base engine mod. The core keeps only the
    /// <c>IInteractiveDialogRunner</c> contract; the manager, the definition
    /// DTOs, the resolver and the factory live in the mod assembly, so every
    /// check here proves the core-to-mod wiring at engine runtime (mod types
    /// are reached by reflection only — the coverage setup compiles inside the
    /// core test project, which cannot reference runtime-compiled mod
    /// assemblies):
    /// <list type="bullet">
    /// <item>migrated types (InteractiveManager/InteractiveDefinition/
    /// InteractiveResolver/InteractiveFactory/InteractiveObject) are loaded
    /// from a mod assembly, and the core assembly no longer defines them;</item>
    /// <item>the mod initializer initialized the manager singleton and wired
    /// the per-scene teardown;</item>
    /// <item>definitions collect from mod data and resolve through the mod
    /// factory, unknown ids degrade gracefully;</item>
    /// <item>the /root/DialogRunner node is reachable through the core
    /// IInteractiveDialogRunner contract (click dialogs + hover DSL).</item>
    /// </list>
    /// The machine-readable line is parsed by <c>tests/Cthangover.TestRunner</c>.
    /// </summary>
    public partial class InteractiveCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "INTERACTIVE_COVERAGE";

        private int _passed;
        private int _failed;

        public override async void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync)
            // before any scene _Ready, so InteractiveModInitializer.OnModResourcesReady
            // has already initialized the manager and wired the scene-exit teardown.
            // The DialogRunner node is parked with CallDeferred though — notify the
            // registry (idempotent) and give the deferred add a frame to flush
            // before the checks below resolve it.
            ModInitializerRegistry.NotifyResourcesReady();
            await ToSignal(GetTree(), "process_frame");
            await ToSignal(GetTree(), "process_frame");

            var kernelAssembly = typeof(GameData).Assembly;

            Check("migrated type InteractiveManager is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Core.Interactive.InteractiveManager", kernelAssembly) != null);
            Check("migrated type InteractiveDefinition is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Core.Interactive.InteractiveDefinition", kernelAssembly) != null);
            Check("migrated type InteractiveResolver is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Core.Mods.Resolvers.InteractiveResolver", kernelAssembly) != null);
            Check("migrated type InteractiveFactory is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Core.Factories.Impls.InteractiveFactory", kernelAssembly) != null);
            Check("migrated type InteractiveObject is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Core.Interactive.InteractiveObject", kernelAssembly) != null);
            Check("core assembly no longer defines InteractiveManager",
                kernelAssembly.GetType("Cthangover.Core.Interactive.InteractiveManager", throwOnError: false) == null);
            Check("core assembly no longer defines InteractiveFactory",
                kernelAssembly.GetType("Cthangover.Core.Factories.Impls.InteractiveFactory", throwOnError: false) == null);

            var remaining = kernelAssembly.GetTypes()
                .Where(t => t.Namespace == typeof(IInteractiveDialogRunner).Namespace && t != typeof(IInteractiveDialogRunner))
                .Select(t => t.FullName)
                .ToList();
            Check("core keeps no Cthangover.Core.Interactive types besides IInteractiveDialogRunner", remaining.Count == 0);

            var manager = SafeGet(() => ResolveManagerSingleton());
            Check("InteractiveManager singleton initialized by the interactive_base initializer", manager != null);

            var count = manager != null ? SafeInt(() => (int)manager.GetType().GetProperty("Count")?.GetValue(manager)) : -1;
            Check("manager tracks no objects after boot", count == 0);

            var sampleId = SafeGet(() => SampleDefinitionId());
            Check("definition collection returns mod data", sampleId != null);

            var resolved = sampleId != null ? SafeGet(() => ResolveDefinition(sampleId)) : null;
            Check("factory resolves a sampled definition from mod data", resolved != null);
            Check("unknown factory id degrades to null (logged, no crash)",
                sampleId == null || ResolveDefinition("__coverage__/missing_interactive") == null);

            if (manager != null)
            {
                var added = SafeGet(() => manager.GetType().GetMethod("Add", new[] { typeof(string) })
                    ?.Invoke(manager, new object[] { "__coverage__/missing_interactive" }));
                Check("manager Add for an unknown id returns null (logged, no crash)", added == null);

                SafeRun(() =>
                {
                    manager.GetType().GetMethod("Remove", new[] { typeof(string) })
                        ?.Invoke(manager, new object[] { "__coverage__/missing_interactive" });
                    manager.GetType().GetMethod("ClearAll", Type.EmptyTypes)?.Invoke(manager, null);
                });
                var afterCount = SafeInt(() => (int)manager.GetType().GetProperty("Count")?.GetValue(manager));
                Check("Remove/ClearAll are safe and leave no tracked objects", afterCount == 0);
            }

            var runner = SafeGet(() => ResolveDialogRunner());
            Check("'/root/DialogRunner' implements the core IInteractiveDialogRunner contract", runner != null);

            GameLogger.Log(Category, $"[TEST_RESULT] name=coverage_interactive passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        /// <summary>Resolves the mod-owned InteractiveManager static singleton (reflection only).</summary>
        private static object ResolveManagerSingleton()
        {
            var kernelAssembly = typeof(GameData).Assembly;
            var managerType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == "Cthangover.Core.Interactive.InteractiveManager");
            return managerType?.GetProperty("Instance")?.GetValue(null);
        }

        /// <summary>Resolves one collected interactive definition id through the mod resolver's static accessor (reflection only).</summary>
        private static string SampleDefinitionId()
        {
            var kernelAssembly = typeof(GameData).Assembly;
            var accessorType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == "Cthangover.Core.Mods.Resolvers.Interactives");
            var collect = accessorType?.GetMethod("Collect", Type.EmptyTypes);
            // Collect() returns Dictionary<string, InteractiveDefinition>; read the
            // key view so the iteration yields definition ids, not KeyValuePairs.
            var dictionary = collect?.Invoke(null, null) as System.Collections.IDictionary;
            if (dictionary == null || dictionary.Keys.Count == 0)
                return null;
            foreach (var key in dictionary.Keys)
                return key as string;
            return null;
        }

        /// <summary>Resolves an interactive definition by id through the mod factory singleton (reflection only).</summary>
        private static object ResolveDefinition(string definitionId)
        {
            var kernelAssembly = typeof(GameData).Assembly;
            var factoryType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == "Cthangover.Core.Factories.Impls.InteractiveFactory");
            var instance = factoryType?.GetProperty("Instance")?.GetValue(null);
            return instance?.GetType().GetMethod("Get", new[] { typeof(string) })
                ?.Invoke(instance, new object[] { definitionId });
        }

        /// <summary>Resolves the dialog runner node parked by dialog_base through the core contract.</summary>
        private static IInteractiveDialogRunner ResolveDialogRunner()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            return tree?.Root?.GetNodeOrNull<Node>("/root/DialogRunner") as IInteractiveDialogRunner;
        }

        private static Type FindOutsideKernel(string fullName, System.Reflection.Assembly kernelAssembly)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == fullName);
        }

        private void Check(string what, bool ok)
        {
            if (ok)
            {
                _passed++;
                GameLogger.Log(Category, $"PASS {what}");
            }
            else
            {
                _failed++;
                GameLogger.Log(Category, $"FAIL {what}", LogLevel.Error);
            }
        }

        private static T SafeGet<T>(Func<T> f) where T : class
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, $"lookup failed: {e.Message}", LogLevel.Error); return null; }
        }

        private static int SafeInt(Func<int> f)
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, $"lookup failed: {e.Message}", LogLevel.Error); return -1; }
        }

        private static void SafeRun(Action f)
        {
            try { f(); }
            catch (Exception e) { GameLogger.Log(Category, $"action failed: {e.Message}", LogLevel.Error); }
        }

        private static Type[] SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }
    }
}
#endif
