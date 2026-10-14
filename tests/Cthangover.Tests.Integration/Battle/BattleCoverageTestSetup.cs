#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Reports integration-test coverage of battle cores. The battle mod's registry
    /// is the single source of truth for how many battle cores the current mod set
    /// ships; <see cref="DeclaredTests"/> maps each core to the runner test type
    /// that drives it. A registered core without a declaration — or without the
    /// mod's IBattleAutoPlayable contract — fails the run, so adding a new core
    /// forces adding its test and the automation contract. The machine-readable
    /// lines are parsed by <c>tests/Cthangover.TestRunner</c>.
    /// <para/>
    /// Registry and contract types live in the turn-battle mod, so this setup goes
    /// through <see cref="TurnBattleBridge"/> instead of referencing them directly.
    /// </summary>
    public partial class BattleCoverageTestSetup : Node
    {
        /// <summary>
        /// Battle core id to the runner test name covering it. Update together
        /// with <c>tests/tests.manifest.json</c> when a core is added.
        /// </summary>
        private static readonly Dictionary<string, string> DeclaredTests = new()
        {
            ["card_battle"] = "battle",
            ["ff_battle"] = "battle_ff",
        };

        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.

            var coreIds = TurnBattleBridge.CoreIds();
            GameLogger.Log("BATTLE_COVERAGE", $"registered cores: {coreIds.Count}");

            foreach (var id in coreIds)
            {
                if (!DeclaredTests.TryGetValue(id, out var testName))
                {
                    Fail($"core={id} has no integration test declared");
                    continue;
                }

                var autoPlayableType = TurnBattleBridge.AutoPlayableType();
                var coreType = TurnBattleBridge.GetCoreType(id);
                if (autoPlayableType == null || coreType == null || !autoPlayableType.IsAssignableFrom(coreType))
                {
                    Fail($"core={id} test={testName} does not implement IBattleAutoPlayable");
                    continue;
                }

                Pass($"core={id} test={testName} autoplayable=true");
            }

            foreach (var declared in DeclaredTests)
            {
                if (!coreIds.Contains(declared.Key))
                    Fail($"declared test='{declared.Value}' references unknown core={declared.Key}");
            }

            var covered = coreIds.Count(id => DeclaredTests.ContainsKey(id));
            GameLogger.Log("BATTLE_COVERAGE", $"coverage: {covered}/{coreIds.Count} cores");
            GameLogger.Log("BATTLE_COVERAGE", $"[TEST_RESULT] name=coverage passed={(_failed == 0 ? 1 : 0)} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private void Pass(string message)
        {
            GameLogger.Log("BATTLE_COVERAGE", $"PASS {message}");
        }

        private void Fail(string message)
        {
            _failed++;
            GameLogger.Log("BATTLE_COVERAGE", $"FAIL {message}", LogLevel.Error);
        }
    }
}
#endif
