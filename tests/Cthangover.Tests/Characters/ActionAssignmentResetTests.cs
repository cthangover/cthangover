using Cthangover.Core.Characters;
using Cthangover.Core.Settings;

namespace Engine.Tests.Characters
{
    public class ActionAssignmentResetTests
    {
        private static Character Template(params string[] actionIds)
        {
            return new Character
            {
                Actions = actionIds.Select(id => new ActionCharacter { ID = id }).ToList()
            };
        }

        [Fact]
        public void Apply_RestoresTemplateSlotsAndKeepsPoolExtras()
        {
            var characterData = new CharacterData();
            characterData.Characters["hero"] = new CharacterInfoData
            {
                CharacterType = "hero",
                ActionSlots = new List<string> { "awarded", null, null },
            };
            characterData.BattleSet.Add("hero");
            var actionPool = new ActionPoolData();
            actionPool.ActionIds.Add("pool_card");

            ActionAssignmentReset.Apply(characterData, actionPool, id => Template("attack", "guard", "heal", "extra"));

            Assert.Equal(new List<string> { "attack", "guard", "heal" }, characterData.Characters["hero"].ActionSlots);
            Assert.Contains("pool_card", actionPool.ActionIds);
            Assert.Contains("extra", actionPool.ActionIds);
        }

        [Fact]
        public void Apply_AwardedCardInSlot_ReturnsToPool()
        {
            var characterData = new CharacterData();
            characterData.Characters["hero"] = new CharacterInfoData
            {
                CharacterType = "hero",
                ActionSlots = new List<string> { "awarded", "attack", null },
            };
            characterData.BattleSet.Add("hero");
            var actionPool = new ActionPoolData();

            ActionAssignmentReset.Apply(characterData, actionPool, id => Template("attack"));

            Assert.Equal("attack", characterData.Characters["hero"].ActionSlots[0]);
            Assert.Contains("awarded", actionPool.ActionIds);
            Assert.DoesNotContain("attack", actionPool.ActionIds);
        }

        [Fact]
        public void Apply_UnknownTemplate_ReturnsAllSlottedCardsToPool()
        {
            var characterData = new CharacterData();
            characterData.Characters["ghost"] = new CharacterInfoData
            {
                CharacterType = "ghost",
                ActionSlots = new List<string> { "rare_card" },
            };
            characterData.BattleSet.Add("ghost");
            var actionPool = new ActionPoolData();

            ActionAssignmentReset.Apply(characterData, actionPool, id => null);

            Assert.Equal(new List<string> { null, null, null }, characterData.Characters["ghost"].ActionSlots);
            Assert.Contains("rare_card", actionPool.ActionIds);
        }

        [Fact]
        public void Apply_NullPool_StillResetsSlots()
        {
            var characterData = new CharacterData();
            characterData.Characters["hero"] = new CharacterInfoData
            {
                CharacterType = "hero",
                ActionSlots = new List<string> { "awarded" },
            };
            characterData.BattleSet.Add("hero");

            ActionAssignmentReset.Apply(characterData, null, id => Template("attack"));

            Assert.Equal(new List<string> { "attack", null, null }, characterData.Characters["hero"].ActionSlots);
        }
    }
}
