#if TOOLS
using System.Linq;
using Cthangover.Core.Factories;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
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

            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.
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
            // The character DTO lives in the characters engine mod, so the
            // kind-to-DTO routing itself is part of the assertion: the kind
            // must resolve to a Roslyn-compiled mod assembly type and still
            // collect exactly the character files.
            var characters = CollectByKind("character");

            AssertTrue(characters != null && characters.Contains("Marao"), "player character resolves through the 'character' kind");
            AssertTrue(characters != null && characters.Contains("wolf_1"), "enemy resolves through the same 'character' kind");
            AssertTrue(characters != null && !characters.Contains("slave_master"), "skill is not collected as a character");
            AssertTrue(characters != null && !characters.Contains("physics/attack"), "action is not collected as a character");
        }

        private void TestActionKind()
        {
            var actions = CollectByKind("action");

            AssertTrue(actions != null && actions.Contains("physics/attack"), "action resolves through the 'action' kind");
            AssertTrue(actions != null && !actions.Contains("Marao"), "character is not collected as an action");
            AssertTrue(actions != null && !actions.Contains("slave_master"), "skill is not collected as an action");
        }

        /// <summary>
        /// Collects every entity of the kind's DTO through reflection: the DTO
        /// type may come from a Roslyn-compiled mod assembly that this
        /// compile-time-referenced test code cannot name directly.
        /// </summary>
        private System.Collections.IDictionary CollectByKind(string kind)
        {
            if (!EntityKindRegistry.TryResolve(kind, out var dtoType))
            {
                AssertTrue(false, $"kind '{kind}' resolves to a DTO type");
                return null;
            }

            var resolver = ModManager.Instance.GetResolver<EntityResolver>();
            var collect = typeof(EntityResolver).GetMethods()
                .First(m => m.Name == nameof(Entities.Collect) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                .MakeGenericMethod(dtoType);
            return (System.Collections.IDictionary)collect.Invoke(resolver, null);
        }

        private void TestSkillKind()
        {
            // The skill DTO lives in the skills_base engine mod, so the kind
            // routing is asserted through the same reflection path as the
            // character/item/recipe kinds above.
            var skills = CollectByKind("skill");

            AssertTrue(skills != null && skills.Contains("slave_master"), "skill resolves through the 'skill' kind");
            AssertTrue(skills == null || !skills.Contains("Marao"), "character is not collected as a skill");
            AssertTrue(skills == null || !skills.Contains("wolf_1"), "enemy is not collected as a skill");
        }

        private void TestItemAndRecipeKinds()
        {
            // The item/recipe DTOs live in the items engine mod, so the kind
            // routing is asserted through the same reflection path as the
            // character kind above.
            var items = CollectByKind("item");
            var recipes = CollectByKind("recipe");

            AssertTrue(items != null && items.Contains("food/ration"), "item resolves through the 'item' kind");
            AssertTrue(items != null && items.Contains("recipe/wolf_meat_to_ration"), "recipe item is an item");
            AssertTrue(recipes != null && recipes.Contains("recipe/wolf_meat_to_ration"), "recipe resolves through the 'recipe' kind");
            AssertTrue(items != null && !items.Contains("AwakeningQuest"), "quest is not collected as an item");
        }

        private void TestQuestKind()
        {
            // The quest DTO lives in the quests engine mod, so the kind
            // routing is asserted through the same reflection path as the
            // character kind above.
            var quests = CollectByKind("quest");

            AssertTrue(quests != null && quests.Contains("AwakeningQuest"), "quest resolves through the 'quest' kind");
            AssertTrue(quests != null && quests.Contains("MeetingMurakamiQuest"), "quest from another mod is collected too");
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
