#if TOOLS
using System;
using System.Linq;
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
    /// <item>"New Game" clears the roster through the IResettable the mod
    /// initializer registers in AppServices.</item>
    /// <item>A second recruit of an already captured enemy template is rejected,
    /// and recruits added afterwards are written into the mod's own save slot.</item>
    /// <item>Adding a recruit to the squad materializes the placeholder core
    /// party card; <c>SyncToCard</c> pushes the mod-owned health onto it.</item>
    /// <item>A second "New Game" wipes the roster, proving the reset is not
    /// dependent on the save file.</item>
    /// <item>CharactersDefeatedEvent drives the roll: the built-in condition gate
    /// blocks recruitment without the <c>slave_master</c> skill and the roll lands
    /// once the gate opens.</item>
    /// <item>Loading the save restores both the roster and the squad membership
    /// (core slot), and the card health is re-synced from the restored recruit.</item>
    /// <item>Leaving the party keeps the card; removing the recruit from the
    /// roster deletes the card and cleans the battle set.</item>
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
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.

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

            RecruitingBridge.Add("test_recruit_dup", "Murakami");
            AssertEqual(2, RecruitingBridge.Count(), "duplicate enemy is not captured twice");
            AssertTrue(RecruitingBridge.HasCharacterId("Murakami"), "roster knows the captured enemy");

            RecruitingBridge.AddToSquad("test_recruit");
            AssertTrue(RecruitingBridge.IsInSquad("test_recruit"), "recruit joins the core party");

            var characterData = gameData.Runtime.CharacterData;
            AssertTrue(characterData.BattleSet.Contains("Murakami"), "battle set contains the recruit");
            AssertTrue(characterData.Characters.ContainsKey("Murakami"), "party card created for the recruit");

            RecruitingBridge.SetHealth("test_recruit", 7);
            RecruitingBridge.SyncToCard("test_recruit");
            AssertEqual(7, characterData.Characters["Murakami"].Attributes.Health.Value,
                "card health synced from the recruit property bag");

            SaveService.Save(SaveName);
            Pass($"saved '{SaveName}'");

            gameData.StartNewGame();
            AssertEqual(0, RecruitingBridge.Count(), "new game clears the roster");

            // The starter initializer seeds slave_master when a new game begins.
            // Clear it to simulate a player who has not learned the skill yet,
            // otherwise the gate below would already be open and the check would
            // depend on the 50% recruit roll.
            gameData.Runtime.SkillData.Skills.Clear();

            PublishDefeated();
            AssertEqual(0, RecruitingBridge.Count(), "condition gate blocks recruitment without slave_master");

            GrantSkillViaMod("slave_master");
            for (var i = 0; i < FlowAttempts; i++)
                PublishDefeated();

            AssertTrue(RecruitingBridge.Count() > 0, "characters_defeated event recruits after the gate opens");

            var loaded = SaveService.Load(SaveName);
            AssertTrue(loaded, "SaveService.Load returns true");
            AssertEqual(2, RecruitingBridge.Count(), "roster restored from the mod slot");

            var characterIds = RecruitingBridge.CharacterIds();
            AssertTrue(characterIds.Contains("Murakami"), "restored 'Murakami'");
            AssertTrue(characterIds.Contains("Marao"), "restored 'Marao'");

            characterData = gameData.Runtime.CharacterData;
            AssertTrue(RecruitingBridge.IsInSquad("test_recruit"), "squad membership restored from the core slot");
            AssertTrue(characterData.BattleSet.Contains("Murakami"), "battle set restored the recruit");
            AssertTrue(characterData.Characters.ContainsKey("Murakami"), "placeholder card restored");
            AssertEqual(7, characterData.Characters["Murakami"].Attributes.Health.Value,
                "card health re-synced from the restored recruit");

            RecruitingBridge.RemoveFromSquad("test_recruit");
            AssertTrue(!RecruitingBridge.IsInSquad("test_recruit"), "recruit leaves the core party");
            AssertTrue(characterData.Characters.ContainsKey("Murakami"), "placeholder card survives leaving the party");

            RecruitingBridge.Remove("test_recruit");
            AssertEqual(1, RecruitingBridge.Count(), "roster drops the removed recruit");
            AssertTrue(!characterData.Characters.ContainsKey("Murakami"), "placeholder card removed with the recruit");
            AssertTrue(!characterData.BattleSet.Contains("Murakami"), "battle set cleaned on recruit removal");

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

        /// <summary>
        /// Grants a skill through the skills_base engine mod's SkillService via
        /// reflection: the service lives in a hot-loaded mod assembly that the
        /// core test code cannot reference at compile time (the same
        /// reflection-only rule every core-side setup follows).
        /// </summary>
        private void GrantSkillViaMod(string skillId)
        {
            var serviceType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => a.GetType("Cthangover.Mods.Skills.SkillService"))
                .FirstOrDefault(t => t != null);
            AssertTrue(serviceType != null, "skills_base mod assembly is loaded");
            if (serviceType == null)
                return;

            var grant = serviceType.GetMethod("GrantSkill", new[] { typeof(string) });
            var granted = (bool)grant.Invoke(null, new object[] { skillId });
            AssertTrue(granted, $"GrantSkill('{skillId}') reports a fresh grant");
        }
    }
}
#endif
