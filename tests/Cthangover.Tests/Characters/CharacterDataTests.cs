using Cthangover.Core.Characters;

namespace Engine.Tests.Characters
{
    /// <summary>
    /// Contract tests for the party roster mutation entry point. The essential
    /// guarantee under test: the roster (<see cref="CharacterData.Characters"/>)
    /// and the battle set (<see cref="CharacterData.BattleSet"/>) change only
    /// together — a battle-set entry without a roster card would crash the
    /// battle scene, which indexes the roster directly. An unknown character
    /// ID must therefore leave both collections untouched and report failure
    /// through the return value instead of silently inserting a zero-stat
    /// placeholder card that would persist through saves.
    /// </summary>
    [Collection("CharacterTemplateRegistryStatic")]
    public class CharacterDataTests : IDisposable
    {
        private readonly StubCharacterTemplateProvider provider = new StubCharacterTemplateProvider();
        private readonly CharacterData data = new CharacterData();

        public CharacterDataTests()
        {
            CharacterTemplateRegistry.Provider = provider;
        }

        public void Dispose()
        {
            CharacterTemplateRegistry.Provider = null;
        }

        [Fact]
        public void AddCharacterToParty_KnownId_ReturnsTrue()
        {
            var added = data.AddCharacterToParty("hero");

            Assert.True(added);
        }

        [Fact]
        public void AddCharacterToParty_KnownId_FillsRosterAndBattleSet()
        {
            data.AddCharacterToParty("hero");

            Assert.True(data.Characters.ContainsKey("hero"));
            Assert.Contains("hero", data.BattleSet);
        }

        [Fact]
        public void AddCharacterToParty_KnownId_CopiesTemplateStatsIntoCard()
        {
            data.AddCharacterToParty("hero");

            var card = data.Characters["hero"];
            Assert.Equal(StubCharacterTemplateProvider.TemplateHealth, card.Attributes.Health.Value);
        }

        [Fact]
        public void AddCharacterToParty_AlreadyInParty_KeepsItsProgress()
        {
            data.AddCharacterToParty("hero");
            var card = data.Characters["hero"];
            card.Level = 7;
            card.Exp = 700;
            card.Attributes.Health.Value = 5;
            data.BattleSet.Remove("hero");

            var added = data.AddCharacterToParty("hero");

            Assert.True(added);
            Assert.Same(card, data.Characters["hero"]);
            Assert.Equal(7, data.Characters["hero"].Level);
            Assert.Equal(700, data.Characters["hero"].Exp);
            Assert.Equal(5, data.Characters["hero"].Attributes.Health.Value);
            Assert.Contains("hero", data.BattleSet);
        }

        [Fact]
        public void AddCharacterToParty_UnknownId_ReturnsFalse()
        {
            provider.UnknownIds.Add("ghost");

            var added = data.AddCharacterToParty("ghost");

            Assert.False(added);
        }

        [Fact]
        public void AddCharacterToParty_UnknownId_LeavesRosterAndBattleSetEmpty()
        {
            provider.UnknownIds.Add("ghost");

            data.AddCharacterToParty("ghost");

            Assert.Empty(data.Characters);
            Assert.Empty(data.BattleSet);
        }

        [Fact]
        public void AddCharacterToParty_ReAddingKnownId_KeepsSingleEntry()
        {
            data.AddCharacterToParty("hero");
            data.AddCharacterToParty("hero");

            Assert.Single(data.Characters);
            Assert.Single(data.BattleSet);
        }
    }
}
