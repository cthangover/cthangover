#if TOOLS
using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration test for the recruiting mod's contracts. It lives in the core
    /// test project because it needs core save infrastructure (SaveService,
    /// ModSaveableRegistry) and reaches the mod-owned roster through
    /// <see cref="RecruitingBridge"/>.
    /// <para/>
    /// The test exercises the full flow:
    /// <list type="number">
    /// <item>"New Game" clears the roster through the NewGameStartedEvent
    /// subscription in the mod initializer.</item>
    /// <item>Recruits added after that are written into the mod's own save slot.</item>
    /// <item>A second "New Game" wipes the roster, proving the reset is not
    /// dependent on the save file.</item>
    /// <item>CharactersDefeatedEvent drives the roll: the built-in condition gate
    /// blocks recruitment without the <c>slave_master</c> skill and the roll lands
    /// once the gate opens.</item>
    /// <item>Loading the save replaces the roster with exactly the saved IDs.</item>
    /// </list>
    /// </summary>
    public partial class RecruitingPersistenceTestSetup : Node
    {
        private const string SaveName = "recruiting_test";
        private const string RecruitableEnemy = "werewolf_girl_1";
        private const int FlowAttempts = 30;

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            if (!ModManager.Instance.IsInitialized)
                ModManager.Instance.Initialize();

            // The initializer's new-game subscription and behaviour scan are
            // normally installed by SceneManager.Initialize; call it explicitly so
            // the test does not depend on autoload timing.
            ModInitializerRegistry.NotifyResourcesReady();

            if (!RecruitingBridge.IsLoaded)
            {
                Fail("recruiting mod assembly is not loaded");
                Finish();
                return;
            }

            var gameData = GameData.Instance;

            gameData.StartNewGame();
            AssertEqual(0, RecruitingBridge.Count(), "roster empty after new game");

            RecruitingBridge.Add("test_recruit", "Murakami");
            RecruitingBridge.Add("test_recruit", "Marao");
            AssertEqual(2, RecruitingBridge.Count(), "roster has two entries after Add");

            SaveService.Save(SaveName);
            Pass($"saved '{SaveName}'");

            gameData.StartNewGame();
            AssertEqual(0, RecruitingBridge.Count(), "new game clears the roster");

            PublishDefeated();
            AssertEqual(0, RecruitingBridge.Count(), "condition gate blocks recruitment without slave_master");

            gameData.Runtime.SkillData.AddSkill("slave_master");
            for (var i = 0; i < FlowAttempts; i++)
                PublishDefeated();

            AssertTrue(RecruitingBridge.Count() > 0, "characters_defeated event recruits after the gate opens");

            var loaded = SaveService.Load(SaveName);
            AssertTrue(loaded, "SaveService.Load returns true");
            AssertEqual(2, RecruitingBridge.Count(), "roster restored from the mod slot");

            var characterIds = RecruitingBridge.CharacterIds();
            AssertTrue(characterIds.Contains("Murakami"), "restored 'Murakami'");
            AssertTrue(characterIds.Contains("Marao"), "restored 'Marao'");

            SaveService.DeleteSave(SaveName);
            Finish();
        }

        private static void PublishDefeated()
        {
            ModEventBus.Publish(new CharactersDefeatedEvent
            {
                Source = "test",
                CharacterIds = new[] { RecruitableEnemy },
            });
        }

        private void AssertEqual(int expected, int actual, string label)
        {
            if (expected == actual)
                Pass($"{label}: {expected}");
            else
                Fail($"{label}: expected {expected}, got {actual}");
        }

        private void AssertTrue(bool condition, string label)
        {
            if (condition)
                Pass($"{label}: true");
            else
                Fail($"{label}: expected true, got false");
        }

        private void Pass(string message)
        {
            _passed++;
            GameLogger.Log("RECRUIT_TEST", $"  [PASS] {message}");
        }

        private void Fail(string message)
        {
            _failed++;
            GameLogger.Log("RECRUIT_TEST", $"  [FAIL] {message}", LogLevel.Error);
        }

        private void Finish()
        {
            GameLogger.Log("RECRUIT_TEST", $"RecruitingPersistenceTestSetup: {_passed} passed, {_failed} failed");
            GameLogger.Log("RECRUIT_TEST", $"[TEST_RESULT] name=recruiting passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }
    }
}
#endif
