using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Scenes;

namespace Engine.Tests.Boot
{
    /// <summary>
    /// Contract tests for the AppServices composition root: the lazy static
    /// Current, SetCurrent publishing, the fixed registry list and the
    /// data-driven loops (ResetForNewGame, ClearRegistries, RebuildRegistries)
    /// with their per-item exception isolation.
    /// <para/>
    /// AppServices.Current is process-wide static state, so every test here
    /// saves the current instance, publishes a fresh one via SetCurrent and
    /// restores the saved instance in a finally block.
    /// </summary>
    [Collection("appservices")]
    public class AppServicesTests
    {
        [Fact]
        public void Current_RepeatedAccess_ReturnsSameInstance()
        {
            var saved = AppServices.Current;
            try
            {
                var first = AppServices.Current;
                var second = AppServices.Current;

                Assert.Same(first, second);
                Assert.Same(saved, second);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void SetCurrent_PublishesNewInstance()
        {
            var saved = AppServices.Current;
            try
            {
                var published = new AppServices();

                AppServices.SetCurrent(published);

                Assert.Same(published, AppServices.Current);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void SetCurrent_NullInstance_ThrowsArgumentNullException()
        {
            var saved = AppServices.Current;
            try
            {
                Assert.Throws<ArgumentNullException>(() => AppServices.SetCurrent(null));
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Registries_ContainsExactlyTenEntries()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                Assert.Equal(10, AppServices.Current.Registries.Count);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Registries_MatchTypedProperties()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());
                var registries = AppServices.Current.Registries;

                Assert.Contains(AppServices.Current.Conflicts, registries);
                Assert.Contains(AppServices.Current.Ui, registries);
                Assert.Contains(AppServices.Current.Menus, registries);
                Assert.Contains(AppServices.Current.SceneEvents, registries);
                Assert.Contains(AppServices.Current.SceneRoutes, registries);
                Assert.Contains(AppServices.Current.SceneDefs, registries);
                Assert.Contains(AppServices.Current.ExtResourceBuilders, registries);
                Assert.Contains(AppServices.Current.PropertyResolvers, registries);
                Assert.Contains(AppServices.Current.SubResourceBuilders, registries);
                Assert.Contains(AppServices.Current.SectionParsers, registries);

                // The ten typed properties are ten distinct instances — no
                // registry is registered twice or missing from the list.
                Assert.Equal(10, registries.Distinct().Count());
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Registries_IdsAreNonEmptyAndUnique()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var ids = AppServices.Current.Registries.Select(r => r.Id).ToList();

                Assert.Equal(10, ids.Count);
                Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
                Assert.Equal(10, ids.Distinct(StringComparer.Ordinal).Count());
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterResettable_AddsOwnerToInstanceList()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var owner = new RecordingResettable("mod_state");
                AppServices.Current.RegisterResettable(owner);

                Assert.Contains(AppServices.Current.Resettables, r => ReferenceEquals(r, owner));
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterResettable_SameId_IsDeduplicated()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var first = new RecordingResettable("dup_owner");
                AppServices.Current.RegisterResettable(first);
                var countAfterFirst = AppServices.Current.Resettables.Count;

                AppServices.Current.RegisterResettable(new RecordingResettable("dup_owner"));

                Assert.Equal(countAfterFirst, AppServices.Current.Resettables.Count);
                Assert.Same(first, AppServices.Current.Resettables.Single(r => r.Id == "dup_owner"));
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterResettable_Null_IsIgnored()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var countBefore = AppServices.Current.Resettables.Count;

                AppServices.Current.RegisterResettable(null);

                Assert.Equal(countBefore, AppServices.Current.Resettables.Count);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void ResetForNewGame_ResetsOwnersInRegistrationOrder()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var calls = new List<string>();
                AppServices.Current.RegisterResettable(new RecordingResettable("alpha", calls));
                AppServices.Current.RegisterResettable(new RecordingResettable("beta", calls));

                AppServices.Current.ResetForNewGame();

                Assert.Equal(new[] { "alpha", "beta" }, calls);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void ResetForNewGame_ExceptionInOneOwner_DoesNotStopTheOthers()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var calls = new List<string>();
                AppServices.Current.RegisterResettable(new RecordingResettable("before_fault", calls));
                AppServices.Current.RegisterResettable(new ThrowingResettable("faulty"));
                AppServices.Current.RegisterResettable(new RecordingResettable("after_fault", calls));

                AppServices.Current.ResetForNewGame();

                Assert.Equal(new[] { "before_fault", "after_fault" }, calls);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void ClearRegistries_ClearsEveryRegistry()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());
                SceneRouteRegistry.Register("boot_test_route", "mods/x/scenes/route.tscn", true);

                AppServices.Current.ClearRegistries();

                Assert.False(SceneRouteRegistry.TryGet("boot_test_route", out _));
                Assert.Null(SceneRouteRegistry.FindByPath("mods/x/scenes/route.tscn"));
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void ClearAndRebuildRegistries_IsolateRegistryExceptions()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                // Outside the Godot runtime several registries cannot rebuild
                // from mods; the loops must swallow that and keep working.
                AppServices.Current.ClearRegistries();
                AppServices.Current.RebuildRegistries();

                // Registries remain usable after the loops.
                SceneRouteRegistry.Register("after_rebuild", "mods/x/scenes/route.tscn", false);
                Assert.True(SceneRouteRegistry.TryGet("after_rebuild", out _));
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void FreshInstance_HasDefaultResettableMapZones()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                // "quests" is registered by the quests_base mod at load time;
                // only map_zones remains a core default outside the runtime.
                var ids = AppServices.Current.Resettables.Select(r => r.Id).ToList();

                Assert.Equal(new[] { "map_zones" }, ids);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterResettable_AvailableBeforeAndAfterSetCurrent()
        {
            var saved = AppServices.Current;
            try
            {
                // Registrations live in the instance, so an owner registered
                // before publication travels with it into Current.
                var standalone = new AppServices();
                standalone.RegisterResettable(new RecordingResettable("before_publish"));

                AppServices.SetCurrent(standalone);
                Assert.Contains(AppServices.Current.Resettables, r => r.Id == "before_publish");

                // And registration keeps working after publication.
                AppServices.Current.RegisterResettable(new RecordingResettable("after_publish"));
                Assert.Contains(AppServices.Current.Resettables, r => r.Id == "after_publish");

                // A replacement instance starts from the map_zones default only.
                AppServices.SetCurrent(new AppServices());
                Assert.Equal(
                    new[] { "map_zones" },
                    AppServices.Current.Resettables.Select(r => r.Id).ToList());
                Assert.DoesNotContain(AppServices.Current.Resettables, r => r.Id == "before_publish");
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        private sealed class RecordingResettable : IResettable
        {
            private readonly List<string> calls;

            public RecordingResettable(string id, List<string> calls = null)
            {
                Id = id;
                this.calls = calls;
            }

            public string Id { get; }

            public int ResetCount { get; private set; }

            public void ResetForNewGame()
            {
                ResetCount++;
                calls?.Add(Id);
            }
        }

        private sealed class ThrowingResettable : IResettable
        {
            public ThrowingResettable(string id)
            {
                Id = id;
            }

            public string Id { get; }

            public void ResetForNewGame()
            {
                throw new InvalidOperationException("reset failed: " + Id);
            }
        }
    }
}
