using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Audio;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;

namespace Engine.Tests.Boot
{
    /// <summary>
    /// Contract tests for the IGameRegistry pattern and for the two pure
    /// registries the static facades delegate to: SceneRouteRegistry
    /// (Register/TryGet/FindByPath/Clear over AppServices.Current.SceneRoutes)
    /// and ModConflictRegistry (Record/Conflicts/GetOverridesBy/GetOverridesOf/
    /// Clear over AppServices.Current.Conflicts).
    /// <para/>
    /// Every test publishes a fresh AppServices instance (Save → SetCurrent →
    /// restore) and clears the registries it touched, so no state leaks into
    /// other tests through the static facades.
    /// </summary>
    [Collection("appservices")]
    public class GameRegistryContractTests
    {
        // ---- IGameRegistry contract (fake implementations) -----------------

        [Fact]
        public void RebuildAndClear_InvocationsAreRecordedInOrder()
        {
            var ops = new List<string>();
            var registry = new FakeGameRegistry("fake_core", ops);

            registry.Rebuild();
            Assert.True(registry.IsFilled);

            registry.Clear();
            Assert.False(registry.IsFilled);

            registry.Rebuild();
            Assert.True(registry.IsFilled);

            Assert.Equal(
                new[] { "rebuild:fake_core", "clear:fake_core", "rebuild:fake_core" },
                ops);
        }

        [Fact]
        public void Clear_ReturnsRegistryToJustConstructedState()
        {
            var registry = new FakeGameRegistry("fake_ui");

            registry.Rebuild();
            registry.Clear();
            Assert.False(registry.IsFilled);

            // Clear is idempotent: the registry stays in its empty state.
            registry.Clear();
            Assert.False(registry.IsFilled);

            // And it rebuilds again from scratch afterwards.
            registry.Rebuild();
            Assert.True(registry.IsFilled);
        }

        [Fact]
        public void RebuildWithoutPrecedingClear_IsSafe()
        {
            var registry = new FakeGameRegistry("fake_routes");

            registry.Rebuild();
            registry.Rebuild();

            Assert.True(registry.IsFilled);
            Assert.Equal(2, registry.RebuildCount);
        }

        // ---- SceneRouteRegistry contract -----------------------------------

        [Fact]
        public void Register_TryGet_RoundTripsRouteWithDefaults()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                SceneRouteRegistry.Register("Arena", "scenes/arena/arena.tscn", true);

                Assert.True(SceneRouteRegistry.TryGet("Arena", out var route));
                Assert.Equal("Arena", route.LogicalName);
                Assert.Equal("scenes/arena/arena.tscn", route.ScenePath);
                Assert.True(route.Fade);
                Assert.Null(route.MusicScene);
                Assert.Equal(MusicType.Ambient, route.Music);
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void TryGet_LogicalNameIsCaseInsensitive()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                SceneRouteRegistry.Register("Arena", "scenes/arena/arena.tscn", false);

                Assert.True(SceneRouteRegistry.TryGet("arena", out var byLowerName));
                Assert.Equal("Arena", byLowerName.LogicalName);
                Assert.True(SceneRouteRegistry.TryGet("ARENA", out _));
                Assert.False(SceneRouteRegistry.TryGet("arena2", out _));
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void TryGet_UnknownOrEmptyName_ReturnsFalseWithNullRoute()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                Assert.False(SceneRouteRegistry.TryGet("missing", out var unknown));
                Assert.Null(unknown);
                Assert.False(SceneRouteRegistry.TryGet("", out _));
                Assert.False(SceneRouteRegistry.TryGet(null, out _));
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Register_SameLogicalName_OverridesEarlierRoute()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                SceneRouteRegistry.Register("Arena", "scenes/arena/arena_old.tscn", false);
                SceneRouteRegistry.Register("Arena", "scenes/arena/arena_new.tscn", true);

                Assert.True(SceneRouteRegistry.TryGet("Arena", out var route));
                Assert.Equal("scenes/arena/arena_new.tscn", route.ScenePath);
                Assert.True(route.Fade);
                Assert.Null(SceneRouteRegistry.FindByPath("scenes/arena/arena_old.tscn"));
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void FindByPath_MatchesScenePathCaseInsensitively()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                SceneRouteRegistry.Register("Arena", "scenes/arena/arena.tscn", true);

                var byPath = SceneRouteRegistry.FindByPath("SCENES/ARENA/ARENA.TSCN");
                Assert.NotNull(byPath);
                Assert.Equal("Arena", byPath.LogicalName);

                Assert.Null(SceneRouteRegistry.FindByPath("scenes/missing.tscn"));
                Assert.Null(SceneRouteRegistry.FindByPath(null));
                Assert.Null(SceneRouteRegistry.FindByPath(""));
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Clear_DropsAllRoutes()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                SceneRouteRegistry.Register("Arena", "scenes/arena/arena.tscn", false);
                SceneRouteRegistry.Register("Shop", "scenes/shop/shop.tscn", false);

                SceneRouteRegistry.Clear();

                Assert.False(SceneRouteRegistry.TryGet("Arena", out _));
                Assert.False(SceneRouteRegistry.TryGet("Shop", out _));
                Assert.Null(SceneRouteRegistry.FindByPath("scenes/shop/shop.tscn"));
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Register_EmptyNameOrPath_IsIgnored()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                SceneRouteRegistry.Register("", "scenes/x.tscn", false);
                SceneRouteRegistry.Register(null, "scenes/x.tscn", false);
                SceneRouteRegistry.Register("Valid", "", false);
                SceneRouteRegistry.Register("Valid", null, false);

                Assert.False(SceneRouteRegistry.TryGet("Valid", out _));
                Assert.Null(SceneRouteRegistry.FindByPath("scenes/x.tscn"));
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void FacadeRegister_MusicSceneAndType_RoundTrip()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                SceneRouteRegistry.Register("Battle", "scenes/battle/battle.tscn", true, "town", MusicType.Combat);

                Assert.True(SceneRouteRegistry.TryGet("Battle", out var route));
                Assert.Equal("scenes/battle/battle.tscn", route.ScenePath);
                Assert.True(route.Fade);
                Assert.Equal("town", route.MusicScene);
                Assert.Equal(MusicType.Combat, route.Music);
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void SceneRouteFacade_WorksExactlyOverAppServicesCurrentSceneRoutes()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                // Registration through the static facade lands in the
                // AppServices-owned registry...
                SceneRouteRegistry.Register("FacadeRoute", "scenes/facade/route.tscn", false);
                Assert.True(SceneRouteRegistry.TryGet("FacadeRoute", out _));

                // ...and clearing the owned registry through IGameRegistry is
                // observed by the facade: the facade and the owned instance
                // are one and the same object.
                ((IGameRegistry)services.SceneRoutes).Clear();
                Assert.False(SceneRouteRegistry.TryGet("FacadeRoute", out _));
                Assert.Null(SceneRouteRegistry.FindByPath("scenes/facade/route.tscn"));
                Assert.False(SceneRouteRegistry.TryGet("FacadeRoute", out _));
            }
            finally
            {
                SceneRouteRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        // ---- ModConflictRegistry contract ----------------------------------

        [Fact]
        public void Record_ConflictsReturnsEntriesWithAllFields()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                ModConflictRegistry.Record("texture", "sword.png", "mod_a", "mod_b", false);
                ModConflictRegistry.Record("file", "data.json", "mod_c", "mod_a", true, "arrays concatenated");

                var entries = ModConflictRegistry.Conflicts;
                Assert.Equal(2, entries.Count);

                var texture = entries.Single(c => c.ResourceType == "texture");
                Assert.Equal("sword.png", texture.ResourceId);
                Assert.Equal("mod_a", texture.WinningModId);
                Assert.Equal("mod_b", texture.OverriddenModId);
                Assert.False(texture.WasConcatenated);
                Assert.Null(texture.Note);

                var file = entries.Single(c => c.ResourceType == "file");
                Assert.Equal("data.json", file.ResourceId);
                Assert.Equal("mod_c", file.WinningModId);
                Assert.Equal("mod_a", file.OverriddenModId);
                Assert.True(file.WasConcatenated);
                Assert.Equal("arrays concatenated", file.Note);
            }
            finally
            {
                ModConflictRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void GetOverridesBy_And_GetOverridesOf_FilterByMod()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                ModConflictRegistry.Record("texture", "a.png", "winner_one", "loser_x", false);
                ModConflictRegistry.Record("file", "b.json", "winner_two", "loser_x", false);
                ModConflictRegistry.Record("scene", "battle.tscn", "winner_one", "loser_y", false);

                var byWinnerOne = ModConflictRegistry.GetOverridesBy("winner_one");
                Assert.Equal(2, byWinnerOne.Count);
                Assert.All(byWinnerOne, c => Assert.Equal("winner_one", c.WinningModId));

                var ofLoserX = ModConflictRegistry.GetOverridesOf("loser_x");
                Assert.Equal(2, ofLoserX.Count);
                Assert.All(ofLoserX, c => Assert.Equal("loser_x", c.OverriddenModId));

                Assert.Empty(ModConflictRegistry.GetOverridesBy("nobody"));
                Assert.Empty(ModConflictRegistry.GetOverridesOf("nobody"));
            }
            finally
            {
                ModConflictRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Clear_RemovesAllConflicts()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                ModConflictRegistry.Record("texture", "a.png", "winner", "loser", false);

                ModConflictRegistry.Clear();

                Assert.Empty(ModConflictRegistry.Conflicts);
            }
            finally
            {
                ModConflictRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void ConflictFacade_IsIdenticalToAppServicesConflicts()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                // Facade Record lands in the AppServices-owned registry...
                ModConflictRegistry.Record("texture", "facade.png", "mod_a", "mod_b", false);
                Assert.Contains(ModConflictRegistry.Conflicts, c => c.ResourceId == "facade.png");

                // ...and clearing the owned registry through IGameRegistry is
                // observed by the facade: the facade and the owned instance
                // are one and the same object.
                ((IGameRegistry)services.Conflicts).Clear();
                Assert.Empty(ModConflictRegistry.Conflicts);
                Assert.Empty(ModConflictRegistry.Conflicts);
            }
            finally
            {
                ModConflictRegistry.Clear();
                AppServices.SetCurrent(saved);
            }
        }

        /// <summary>
        /// Fake IGameRegistry that records every Rebuild/Clear invocation into
        /// a shared log, mirroring how AppServices consumes the interface.
        /// </summary>
        private sealed class FakeGameRegistry : IGameRegistry
        {
            private readonly List<string> ops;

            public FakeGameRegistry(string id, List<string> ops = null)
            {
                Id = id;
                this.ops = ops;
            }

            public string Id { get; }

            public bool IsFilled { get; private set; }

            public int RebuildCount { get; private set; }

            public int ClearCount { get; private set; }

            public void Rebuild()
            {
                RebuildCount++;
                IsFilled = true;
                ops?.Add("rebuild:" + Id);
            }

            public void Clear()
            {
                ClearCount++;
                IsFilled = false;
                ops?.Add("clear:" + Id);
            }
        }
    }
}
