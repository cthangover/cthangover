#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System.Collections.Generic;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Reports integration-test coverage of the migrated gameplay subsystems.
    /// Generalizes <see cref="BattleCoverageTestSetup"/> from battle cores to
    /// every subsystem that leaves the core assembly during the "core = mod
    /// player" migration: a subsystem's mod registry (the
    /// <c>SubsystemRegistration</c> marker, see <see cref="SubsystemsBridge"/>) is
    /// the single source of truth for what is already migrated, and
    /// <see cref="DeclaredTests"/> maps each subsystem to the runner test that
    /// covers it. A migrated subsystem without a declared test fails the run, so
    /// moving a subsystem forces adding its test. The machine-readable line is
    /// parsed by <c>tests/Cthangover.TestRunner</c>.
    /// <para/>
    /// Transitional state: while a declared subsystem has not moved yet, its
    /// registration is simply absent — the run stays green and the subsystem is
    /// reported as skipped, so an empty or partially migrated mod set passes.
    /// </summary>
    public partial class SubsystemsCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "SUBSYSTEM_COVERAGE";

        /// <summary>
        /// Subsystem id to the runner test name covering it. Update together
        /// with <c>tests/tests.manifest.json</c> when a subsystem finishes its
        /// migration (the id must match the marker's SubsystemId).
        /// </summary>
        private static readonly Dictionary<string, string> DeclaredTests = new()
        {
            ["items"] = "coverage_items",
            ["quests"] = "coverage_quests",
            ["characters"] = "coverage_characters",
            ["interactive"] = "coverage_interactive",
            ["skills"] = "coverage_skills",
            ["time"] = "coverage_time",
            ["map"] = "coverage_map",
            ["effects"] = "coverage_effects",
            ["dialog"] = "coverage_dialog",
            ["audio"] = "coverage_audio",
        };

        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.

            var registrations = SubsystemsBridge.FindRegistrations();
            GameLogger.Log(Category, $"registered subsystems: {registrations.Count}");
            GameLogger.Log(Category, $"declared tests: {DeclaredTests.Count}");

            var manifestTests = TestManifestNames.Load();
            var migrated = new HashSet<string>();
            foreach (var registration in registrations)
            {
                if (string.IsNullOrEmpty(registration.SubsystemId))
                {
                    Fail($"mod assembly '{registration.AssemblyName}' ships SubsystemRegistration without a readable SubsystemId");
                    continue;
                }

                if (!migrated.Add(registration.SubsystemId))
                {
                    Fail($"duplicate SubsystemRegistration for subsystem='{registration.SubsystemId}' in '{registration.AssemblyName}'");
                    continue;
                }

                if (!DeclaredTests.TryGetValue(registration.SubsystemId, out var testName))
                {
                    Fail($"subsystem='{registration.SubsystemId}' (mod '{registration.AssemblyName}') is registered but has no declared integration test — " +
                         "add it to DeclaredTests and tests/tests.manifest.json");
                    continue;
                }

                if (!manifestTests.Contains(testName))
                {
                    Fail($"subsystem='{registration.SubsystemId}' declares test '{testName}', which tests/tests.manifest.json does not run");
                    continue;
                }

                Pass($"subsystem={registration.SubsystemId} test={testName} mod={registration.AssemblyName}");
            }

            foreach (var declared in DeclaredTests)
            {
                if (!migrated.Contains(declared.Key))
                    GameLogger.Log(Category, $"SKIP subsystem={declared.Key} test={declared.Value}: not migrated yet (no SubsystemRegistration in loaded mods)");
            }

            GameLogger.Log(Category, $"[TEST_RESULT] name=coverage_subsystems passed={(_failed == 0 ? 1 : 0)} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private void Pass(string message)
        {
            GameLogger.Log(Category, $"PASS {message}");
        }

        private void Fail(string message)
        {
            _failed++;
            GameLogger.Log(Category, $"FAIL {message}", LogLevel.Error);
        }
    }
}
#endif
