#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Linq;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage for the items subsystem migrated into the
    /// items_base engine mod. The core keeps only the contracts
    /// (<c>IInventory</c>, <c>IRecipeData</c>, the item interfaces plus the
    /// GD wrappers and <c>ItemActionBridge</c>); the factories, the concrete
    /// inventory/recipe state and the inventory UI live in the mod assembly,
    /// so every check here proves the core-to-mod wiring at engine runtime:
    /// <list type="bullet">
    /// <item>the mod initializer installed the <c>RuntimeData</c> factory
    /// hooks, so the state bag serves a mod-owned inventory and recipe
    /// state;</item>
    /// <item>an id-string add resolves the item through the mod's
    /// <c>ItemFactory</c> and removes again through the core contract;</item>
    /// <item>migrated types (ItemFactory/ItemActionFactory) are loaded from a
    /// mod assembly, and the core assembly no longer defines them;</item>
    /// <item>a foreign mod's <c>IItemAction</c> (cooking) reached the items
    /// factory through the mod's <c>ItemActionConsumer</c>.</item>
    /// </list>
    /// The machine-readable line is parsed by <c>tests/Cthangover.TestRunner</c>.
    /// </summary>
    public partial class ItemsCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "ITEMS_COVERAGE";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync)
            // before any scene _Ready, so ItemsModInitializer.OnModResourcesReady has
            // already installed the factory hooks and bound the action bridge.

            var kernelAssembly = typeof(GameData).Assembly;

            var inventory = SafeGet(() => GameData.Instance.Runtime.Inventory);
            Check("RuntimeData.Inventory is produced by the items mod factory hook", inventory != null);
            Check("inventory implementation comes from a mod assembly, not the core",
                inventory != null && inventory.GetType().Assembly != kernelAssembly);

            var recipes = SafeGet(() => GameData.Instance.Runtime.RecipeData);
            Check("RuntimeData.RecipeData is produced by the items mod factory hook", recipes != null);
            Check("recipe state implementation comes from a mod assembly, not the core",
                recipes != null && recipes.GetType().Assembly != kernelAssembly);
            Check("recipe state answers Has through the core contract",
                recipes != null && !recipes.Has("recipe/never_unlocked"));

            // The id-string path resolves the item through the mod's
            // ItemFactory; the mutation is throwaway scene-local state.
            SafeRun(() => inventory.Add("food/ration", 2));
            Check("id-string add resolves the item through the mod ItemFactory",
                SafeInt(() => inventory.CheckCount("food/ration")) == 2);
            Check("remove through the core contract drops the stack",
                SafeBool(() => inventory.Remove("food/ration", 2)));

            // The migrated sources keep their core namespace, but the types
            // must be loaded from mod assemblies compiled at runtime, not from
            // the core.
            Check("migrated type ItemFactory is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Core.Factories.Impls.ItemFactory", kernelAssembly) != null);
            Check("migrated type Inventory is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Core.Items.Inventory", kernelAssembly) != null);
            Check("core assembly no longer defines ItemFactory",
                kernelAssembly.GetType("Cthangover.Core.Factories.Impls.ItemFactory", throwOnError: false) == null);
            Check("core assembly no longer defines Inventory",
                kernelAssembly.GetType("Cthangover.Core.Items.Inventory", throwOnError: false) == null);

            // A foreign mod's IItemAction (cooking) reaches the items factory
            // through the items mod's ItemActionConsumer.
            Check("cooking mod's IItemAction was delivered through ItemActionConsumer",
                SafeGet(() => ResolveAction("action/recipe/wolf_meat_to_ration")) != null);

            // ─── Inventory panel scene contract (tscn migration) ───
            // The panel layout lives in inventory_panel.tscn; the behaviour
            // resolves its nodes with hard GetNode paths in OnceConstruct.
            CallDeferred(nameof(RunPanelSceneCheck));
        }

        private void RunPanelSceneCheck()
        {
            var ok = false;
            var error = "";
            try
            {
                var panel = new ModScenePrefab("scenes/ui/inventory_panel.tscn").Instantiate<Control>();
                AddChild(panel);
                // Widget.Show is hidden behind the Control reference — invoke it
                // on the actual mod type so the widget lifecycle runs.
                panel.GetType().GetMethod("Show", Type.EmptyTypes)?.Invoke(panel, null);

                var hasGrid = panel.GetNodeOrNull("Panel/Margin/Root/Body/Left/GridScroll/GridContent") != null;
                var passport = panel.GetNodeOrNull<VBoxContainer>("Panel/Margin/Root/Body/Right/PassportScroll/Passport");
                var passportBuilt = passport != null && passport.GetChildCount() > 0;
                var tabsBuilt = panel.GetNode<HBoxContainer>("Panel/Margin/Root/Header/Tabs").GetChildCount() > 0;

                panel.GetType().GetMethod("Hide", Type.EmptyTypes)?.Invoke(panel, null);
                panel.QueueFree();

                error = $"grid={hasGrid} passportBuilt={passportBuilt} tabsBuilt={tabsBuilt}";
                ok = hasGrid && passportBuilt && tabsBuilt;
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            Check("inventory panel scene instantiates and builds: " + error, ok);

            GameLogger.Log(Category, $"[TEST_RESULT] name=coverage_items passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        /// <summary>Resolves a registered item action through the mod-owned ItemActionFactory singleton (reflection only).</summary>
        private static object ResolveAction(string actionId)
        {
            var kernelAssembly = typeof(GameData).Assembly;
            var factoryType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly)
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == "Cthangover.Core.Factories.Impls.ItemActionFactory");
            if (factoryType == null)
                return null;

            var instance = factoryType.GetProperty("Instance")?.GetValue(null);
            return instance?.GetType().GetMethod("Get", new[] { typeof(string) })
                ?.Invoke(instance, new object[] { actionId });
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

        private static bool SafeBool(Func<bool> f)
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, $"lookup failed: {e.Message}", LogLevel.Error); return false; }
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
