namespace Cthangover.Tests.Games
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods;

    /// <summary>A computed mod-set choice becomes the process-wide active game only when applied.</summary>
    [Collection("active-game")]
    public class ActiveGameChoiceTests : IDisposable
    {
        public void Dispose() => ActiveGame.Clear();

        private static GameModSet Set(ModCollectionKind kind)
        {
            var story = new ModInfo { Id = "story", Name = "story", Manifest = new ModManifest { Id = "story", Name = "story", Version = "1.0.0", Depends = new List<ModDependency>() } };
            var collection = new ModCollection
            {
                Id = "novel",
                Kind = kind,
                Entry = new ModCollectionEntry { Scene = "intro" },
                Mechanics = new List<ModCollectionMechanic> { new() { Id = "core", Mods = new List<string> { "story" } } },
            };
            var collections = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase) { ["novel"] = collection };
            var mods = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase) { ["story"] = story };
            return kind == ModCollectionKind.Frontend
                ? GameModSet.ComputeFrontend(collections, mods, Array.Empty<string>())
                : GameModSet.Compute("novel", collections, mods, Array.Empty<string>());
        }

        [Fact]
        public void Apply_Game_ActivatesIt()
        {
            var choice = new ActiveGameChoice(Set(ModCollectionKind.Game), frontend: false);

            Assert.False(ActiveGame.IsActive);
            choice.Apply();

            Assert.True(ActiveGame.IsActive);
            Assert.False(ActiveGame.IsFrontendOnly);
            Assert.Equal("novel", ActiveGame.Id);
            Assert.Equal("intro", ActiveGame.EntryScene);
            Assert.Contains("story", ActiveGame.ModIds);
        }

        [Fact]
        public void Apply_Frontend_MarksFrontendOnly()
        {
            new ActiveGameChoice(Set(ModCollectionKind.Frontend), frontend: true).Apply();

            Assert.True(ActiveGame.IsFrontendOnly);
            Assert.False(ActiveGame.IsActive);
        }

        [Fact]
        public void Apply_None_ClearsTheActiveGame()
        {
            new ActiveGameChoice(Set(ModCollectionKind.Game), frontend: false).Apply();

            ActiveGameChoice.None.Apply();

            Assert.False(ActiveGame.IsActive);
            Assert.False(ActiveGame.IsFrontendOnly);
            Assert.Null(ActiveGameChoice.None.Set);
            Assert.False(ActiveGameChoice.None.Frontend);
        }
    }
}
