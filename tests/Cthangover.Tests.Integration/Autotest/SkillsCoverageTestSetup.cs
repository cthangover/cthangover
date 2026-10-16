#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Linq;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage for the skills subsystem migrated into the
    /// skills_base engine mod. The grant service, the factory and the skills
    /// UI live in the mod assembly, the core keeps the SkillData transition
    /// DTO and reaches granting through SkillsBridge, so every check here
    /// proves the core-to-mod wiring at engine runtime.
    /// The machine-readable line is parsed by <c>tests/Cthangover.TestRunner</c>.
    /// </summary>
    public partial class SkillsCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "SKILLS_COVERAGE";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync)
            // before any scene _Ready, so the subsystem's OnModResourcesReady has
            // already installed its hooks.

            var kernelAssembly = typeof(GameData).Assembly;

            Check("migrated type SkillFactory is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Mods.Skills.SkillFactory", kernelAssembly) != null);
            Check("core assembly no longer defines SkillFactory",
                KernelLacksTypeNamed(kernelAssembly, "SkillFactory"));

            Check("migrated type SkillService is loaded from a mod assembly",
                FindOutsideKernel("Cthangover.Mods.Skills.SkillService", kernelAssembly) != null);
            Check("core assembly no longer defines SkillService",
                KernelLacksTypeNamed(kernelAssembly, "SkillService"));

            Check("transition DTO SkillData still lives in the core data model",
                kernelAssembly.GetType("Cthangover.Core.Skills.SkillData") != null);

            var bridgeType = kernelAssembly.GetType("Cthangover.Core.Actions.SkillsBridge");
            var grantHook = bridgeType?.GetField("_grantSkill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
            Check("skills_base installed the SkillsBridge grant hook", grantHook != null);
            Check("grant through the core bridge answers false for an unknown id",
                !Cthangover.Core.Actions.SkillsBridge.Grant("skill/__coverage_missing__"));

            GameLogger.Log(Category, $"[TEST_RESULT] name=skills_coverage passed={_passed} failed={_failed}");
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
