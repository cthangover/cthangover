namespace Engine.Tests.Mods
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;

    /// <summary>Version editing is scoped to one mechanic and persists through the collection's real JSON format.</summary>
    public class ModCollectionMechanicTests
    {
        [Theory]
        [InlineData("", "ignored", "app")]
        [InlineData(">=", "2.0", "app>=2.0")]
        [InlineData("=", "1.2.3", "app=1.2.3")]
        [InlineData("^", "1.2", "app^1.2")]
        [InlineData("<=", "3", "app<=3")]
        [InlineData(">", "1", "app>1")]
        [InlineData("<", "4", "app<4")]
        public void TrySetModVersion_UpdatesOnlyTheRequestedMember(string op, string version, string expected)
        {
            var mechanic = new ModCollectionMechanic { Mods = new List<string> { "other>=1", "app>=1" } };

            Assert.True(mechanic.TrySetModVersion("app", op, version));

            Assert.Equal(new[] { "other>=1", expected }, mechanic.Mods);
        }

        [Theory]
        [InlineData("~", "1")]
        [InlineData(">=", "")]
        [InlineData("=", "1.0?")]
        [InlineData("=", "1 2")]
        [InlineData("=", "../2")]
        public void TrySetModVersion_InvalidRule_DoesNotMutateTheMechanic(string op, string version)
        {
            var mechanic = new ModCollectionMechanic { Mods = new List<string> { "app>=1" } };

            Assert.False(mechanic.TrySetModVersion("app", op, version));

            Assert.Equal(new[] { "app>=1" }, mechanic.Mods);
        }

        [Fact]
        public void TrySetModVersion_MissingMember_IsNotAddedImplicitly()
        {
            var mechanic = new ModCollectionMechanic { Mods = new List<string> { "other>=1" } };

            Assert.False(mechanic.TrySetModVersion("app", "=", "2"));

            Assert.Equal(new[] { "other>=1" }, mechanic.Mods);
        }

        [Fact]
        public void TrySetModVersion_PreservesSuffixAndOtherGroupsThroughJsonRoundTrip()
        {
            var collection = new ModCollection
            {
                Id = "game",
                Settings = new Dictionary<string, string> { ["battle.core"] = "app" },
                Mechanics = new List<ModCollectionMechanic>
                {
                    new() { Id = "first", Mods = new List<string> { "app>=1?", "other>=1" }, Optional = true, Selected = false },
                    new() { Id = "second", Mods = new List<string> { "app>=1" } },
                },
            };

            Assert.True(collection.Mechanics[0].TrySetModVersion("app", "=", "2.0.0"));
            var json = JsonSerializer.Serialize(collection, JsonOptionsProvider.WriteIndentedOptions);
            var restored = JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);

            Assert.Equal("game", restored.Id);
            Assert.Equal("app", restored.Settings["battle.core"]);
            Assert.Equal(new[] { "app=2.0.0?", "other>=1" }, restored.Mechanics[0].Mods);
            Assert.True(restored.Mechanics[0].Optional);
            Assert.False(restored.Mechanics[0].IsSelected);
            Assert.Equal(new[] { "app>=1" }, restored.Mechanics[1].Mods);
        }
    }
}
