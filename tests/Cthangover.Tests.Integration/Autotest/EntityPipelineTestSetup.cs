#if TOOLS
using Cthangover.Core.Characters;
using Cthangover.Core.Factories;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Quests;
using Cthangover.Core.Skills;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration test for the kind-routed entity pipeline. It locks the core
    /// invariant of the <c>EntityResolver</c>: a file is collected only under
    /// the DTO whose <c>[EntityKind]</c> matches the file's declared
    /// <c>"Kind"</c>, no matter which directory it lives in. The old
    /// directory-typed pipeline fed characters, enemies, actions and skills
    /// through the same <c>characters/</c> folder, and each factory absorbed
    /// its neighbours' definitions; these assertions fail loudly if that
    /// cross-type bleed ever returns.
    /// <para/>
    /// The test also covers DTOs that live in Roslyn-compiled mod assemblies:
    /// the <c>status_effect</c> kind must be visible to the registry after
    /// mod loading, otherwise the scan would reject the file as unknown.
    /// </summary>
    public partial class EntityPipelineTestSetup : Node
    {
        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            GameLogger.Log("ENTITY_TEST", "EntityPipelineTestSetup: starting entity pipeline tests");

            ModManager.Instance.Initialize();
            ModInitializerRegistry.NotifyResourcesReady();

            TestCharacterKind();
            TestActionKind();
            TestSkillKind();
            TestItemAndRecipeKinds();
            TestQuestKind();
            TestModAssemblyKind();

            GameLogger.Log("ENTITY_TEST", $"EntityPipelineTestSetup: {_passed} passed, {_failed} failed");
            GameLogger.Log("ENTITY_TEST", $"[TEST_RESULT] name=entities passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private void TestCharacterKind()
        {
            var characters = Entities.Collect<CharacterInfo>();

            AssertTrue(characters.ContainsKey("Marao"), "player character resolves through the 'character' kind");
            AssertTrue(characters.ContainsKey("wolf_1"), "enemy resolves through the same 'character' kind");
            AssertTrue(!characters.ContainsKey("slave_master"), "skill is not collected as a character");
            AssertTrue(!characters.ContainsKey("physics/attack"), "action is not collected as a character");
        }

        private void TestActionKind()
        {
            var actions = Entities.Collect<ActionCharacterInfo>();

            AssertTrue(actions.ContainsKey("physics/attack"), "action resolves through the 'action' kind");
            AssertTrue(!actions.ContainsKey("Marao"), "character is not collected as an action");
            AssertTrue(!actions.ContainsKey("slave_master"), "skill is not collected as an action");
        }

        private void TestSkillKind()
        {
            var skills = Entities.Collect<SkillInfo>();

            AssertTrue(skills.ContainsKey("slave_master"), "skill resolves through the 'skill' kind");
            AssertTrue(!skills.ContainsKey("Marao"), "character is not collected as a skill");
            AssertTrue(!skills.ContainsKey("wolf_1"), "enemy is not collected as a skill");
        }

        private void TestItemAndRecipeKinds()
        {
            var items = Entities.Collect<ItemInfo>();
            var recipes = Entities.Collect<RecipeInfo>();

            AssertTrue(items.ContainsKey("food/ration"), "item resolves through the 'item' kind");
            AssertTrue(items.ContainsKey("recipe/wolf_meat_to_ration"), "recipe item is an item");
            AssertTrue(recipes.ContainsKey("recipe/wolf_meat_to_ration"), "recipe resolves through the 'recipe' kind");
            AssertTrue(!items.ContainsKey("AwakeningQuest"), "quest is not collected as an item");
        }

        private void TestQuestKind()
        {
            var quests = Entities.Collect<QuestDefinition>();

            AssertTrue(quests.ContainsKey("AwakeningQuest"), "quest resolves through the 'quest' kind");
            AssertTrue(quests.ContainsKey("MeetingMurakamiQuest"), "quest from another mod is collected too");
        }

        private void TestModAssemblyKind()
        {
            AssertTrue(EntityKindRegistry.IsKnown("status_effect"),
                "status_effect kind from a Roslyn-compiled mod assembly is registered");

            var statusText = ModManager.Instance.ReadFileText("turn_battle_base", "status_effects/status_effects.json");
            var peeked = EntityKindPeek.TryRead(statusText, out var kind);
            AssertTrue(peeked && kind == "status_effect", "status effect file self-describes as 'status_effect'");
        }

        private void AssertTrue(bool condition, string label)
        {
            if (condition)
            {
                _passed++;
                GameLogger.Log("ENTITY_TEST", $"  [PASS] {label}");
            }
            else
            {
                _failed++;
                GameLogger.Log("ENTITY_TEST", $"  [FAIL] {label}", LogLevel.Error);
            }
        }
    }
}
#endif
