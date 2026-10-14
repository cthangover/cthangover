#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Linq;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI.Dialog;
    using Cthangover.Core.UI.Executable;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage for the dialog subsystem migrated into the
    /// dialog_base engine mod. The core keeps only the transition contracts
    /// (<c>IDialogBox</c>, the executable event contracts,
    /// <c>IScenarioEventFactory</c>, <c>ScenarioMetadata</c> and the
    /// <c>ScenarioCommandBridge</c>); the dialog UI, the DSL parser, the
    /// command strategies and the executable event engine live in the mod
    /// assembly, so every check here proves the core-to-mod wiring at engine
    /// runtime:
    /// <list type="bullet">
    /// <item>the mod initializer installed the scenario event factory into
    /// <c>AppServices.Current.SceneEvents</c>, and the factory implementation
    /// comes from a mod assembly;</item>
    /// <item>the factory produces executable events through the core
    /// <c>IScenarioEventFactory</c> contract;</item>
    /// <item>migrated types (DialogBox, the DSL parser, the command strategy
    /// factory) are loaded from a mod assembly, and the core assembly no
    /// longer defines them;</item>
    /// <item>the DSL command vocabulary resolved into the mod factory (the
    /// ScenarioCommandBridge delivery point);</item>
    /// <item>the SubsystemRegistration marker declares the dialog subsystem
    /// for the coverage pipeline.</item>
    /// </list>
    /// The machine-readable line is parsed by <c>tests/Cthangover.TestRunner</c>.
    /// </summary>
    public partial class DialogCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "DIALOG_COVERAGE";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync)
            // before any scene _Ready, so DialogModInitializer.OnModResourcesReady has
            // already installed the event factory and the strategy bridge.

            var kernelAssembly = typeof(GameData).Assembly;

            var factory = AppServices.Current.SceneEvents.ScenarioEventFactory;
            Check("IScenarioEventFactory installed by the dialog mod initializer", factory != null);
            Check("event factory implementation comes from a mod assembly, not the core",
                factory != null && factory.GetType().Assembly != kernelAssembly);

            var evt = SafeGet(() => factory.CreateFromText("background black"));
            Check("factory produces an executable event through the core contract", evt != null);

            Check("migrated type DialogBox is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Mods.Dialog.DialogBox", kernelAssembly) != null);
            Check("migrated type ScenarioParser is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Mods.Dialog.Scenarios.ScenarioParser", kernelAssembly) != null);
            Check("core assembly no longer defines DialogBox",
                kernelAssembly.GetType("Cthangover.Core.UI.Dialog.DialogBox", throwOnError: false) == null);
            Check("core assembly no longer defines ScenarioParser",
                kernelAssembly.GetType("Cthangover.Core.Scenarios.ScenarioParser", throwOnError: false) == null);

            var commandCount = SafeInt(() => CountCommands());
            Check($"DSL command vocabulary resolved through ScenarioCommandBridge (commands={commandCount})",
                commandCount >= 20);

            Check("SubsystemRegistration declares subsystem id 'dialog'",
                SafeGet(() => GetSubsystemId() == "dialog"));

            Check("IDialogBox transition contract stays in the core assembly",
                kernelAssembly.GetType("Cthangover.Core.UI.Dialog.IDialogBox", throwOnError: false) != null);

            GameLogger.Log(Category, $"[TEST_RESULT] name=coverage_dialog passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        /// <summary>Counts the DSL commands registered in the mod-owned ScenarioCommandStrategyFactory (reflection only).</summary>
        private static int CountCommands()
        {
            var kernelAssembly = typeof(GameData).Assembly;
            var factoryType = FindOutsideKernel("Cthangover.Mods.Dialog.Scenarios.ScenarioCommandStrategyFactory", kernelAssembly);
            if (factoryType == null)
                return -1;

            var method = factoryType.GetMethod("GetAllCommandNames", Type.EmptyTypes);
            if (method == null)
                return -1;

            var raw = method.Invoke(null, null) as System.Collections.IEnumerable;
            if (raw == null)
                return -1;

            return raw.Cast<object>().Count();
        }

        /// <summary>Reads SubsystemId off the mod's SubsystemRegistration marker (reflection only).</summary>
        private static string GetSubsystemId()
        {
            var kernelAssembly = typeof(GameData).Assembly;
            var markerType = FindOutsideKernel("Cthangover.Mods.Dialog.SubsystemRegistration", kernelAssembly);
            return markerType?.GetProperty("SubsystemId")?.GetValue(null) as string;
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

        private static T SafeGet<T>(Func<T> f)
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, $"lookup failed: {e.Message}", LogLevel.Error); return default; }
        }

        private static int SafeInt(Func<int> f)
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, $"lookup failed: {e.Message}", LogLevel.Error); return -1; }
        }

        private static Type[] SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }
    }
}
#endif
