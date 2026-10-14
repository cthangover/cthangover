using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Scenes;
using Xunit;

namespace Engine.Tests.Boot
{
    /// <summary>
    /// Lifecycle contracts of the converted registries (auditor follow-up):
    /// imperative registrations vs forced rebuilds, and thread-safety of the
    /// AppServices registration loop. Deliberately no SceneDefinitions.Collect
    /// call here: outside the Godot runtime it would trigger a full lazy mod
    /// load; only the cheap Clear/Rebuild paths are exercised.
    /// </summary>
    [Collection("appservices")]
    public class RegistryLifecycleTests
    {
        [Fact]
        public void SceneEventRegistry_RegisterAfterClear_WorksUntilNextRebuild()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                SceneEventRegistry.Register(typeof(string), "lifecycle_scene");
                Assert.Single(SceneEventRegistry.GetEvents("lifecycle_scene"));

                // Clear wipes imperative registrations; the next GetEvents
                // re-scans only the attribute-decorated types.
                ((IGameRegistry)AppServices.Current.SceneEvents).Clear();
                Assert.Empty(SceneEventRegistry.GetEvents("lifecycle_scene"));

                SceneEventRegistry.Register(typeof(string), "lifecycle_scene");
                Assert.Single(SceneEventRegistry.GetEvents("lifecycle_scene"));

                // A forced rebuild resets imperative registrations by contract
                // (mod initializers must re-register afterwards).
                AppServices.Current.SceneEvents.Rebuild();
                Assert.Empty(SceneEventRegistry.GetEvents("lifecycle_scene"));

                // Re-registration after a rebuild works exactly as before.
                SceneEventRegistry.Register(typeof(string), "lifecycle_scene");
                Assert.Single(SceneEventRegistry.GetEvents("lifecycle_scene"));
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void SceneDefinitions_ClearAndFacadeIdentity_NoCollectOutsideRuntime()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var owned = AppServices.Current.SceneDefs;
                Assert.Equal("scene_defs", owned.Id);
                Assert.Contains(owned, AppServices.Current.Registries);

                // Clear and Rebuild paths stay no-throw outside the runtime
                // (Collect itself is intentionally not called here: it would
                // trigger a full lazy mod load in unit tests).
                ((IGameRegistry)owned).Clear();
                owned.Rebuild();
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public async Task RegisterResettable_ConcurrentWithReset_IsSnapshotSafe()
        {
            var saved = AppServices.Current;
            try
            {
                AppServices.SetCurrent(new AppServices());

                var ids = Enumerable.Range(0, 200).Select(i => "concurrent_" + i).ToList();
                var registerTasks = ids
                    .Select(id => Task.Run(() => AppServices.Current.RegisterResettable(new CountingResettable(id))))
                    .ToList();
                var resetTask = Task.Run(() =>
                {
                    for (var i = 0; i < 20; i++)
                        AppServices.Current.ResetForNewGame();
                });

                await Task.WhenAll(registerTasks.Concat(new[] { resetTask }));

                // Every registration survives the concurrent resets (the
                // map_zones default + 200 concurrent ones) and the reset loop
                // never threw (a thrown Task.WhenAll would fail this test).
                Assert.Equal(201, AppServices.Current.Resettables.Count);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        private sealed class CountingResettable : IResettable
        {
            public CountingResettable(string id)
            {
                Id = id;
            }

            public string Id { get; }

            public void ResetForNewGame()
            {
                // Intentionally empty: the contract under test is the snapshot
                // and registration loop, not any particular state owner.
            }
        }
    }
}
