using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;

namespace Engine.Tests.Boot
{
    /// <summary>
    /// Contract tests for IResettable registration through
    /// AppServices.RegisterResettable — the entry point a mod's
    /// IModInitializer uses to hook new-game state resets — plus the
    /// duplicate-Id dedup rules of the registration list.
    /// <para/>
    /// Same static-state discipline as AppServicesTests: fresh SetCurrent per
    /// test inside the shared "appservices" collection, restore in finally.
    /// </summary>
    [Collection("appservices")]
    public class ResettableTests
    {
        [Fact]
        public void RegisterResettable_ModStyleOwner_IsResetOnNewGame()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                // Simulates a mod initializer registering its playthrough
                // state owner after boot published the service set.
                var stateCleared = false;
                var modOwner = new TestResettable("mod_roster", () => stateCleared = true);
                services.RegisterResettable(modOwner);

                AppServices.Current.ResetForNewGame();

                Assert.True(stateCleared);
                Assert.Equal(1, modOwner.ResetCount);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterResettable_DuplicateId_SecondRegistrationIgnored()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var first = new TestResettable("mod_roster");
                var duplicate = new TestResettable("mod_roster");

                services.RegisterResettable(first);
                services.RegisterResettable(duplicate);

                AppServices.Current.ResetForNewGame();

                Assert.Equal(1, first.ResetCount);
                Assert.Equal(0, duplicate.ResetCount);
                Assert.Single(services.Resettables.Where(r => r.Id == "mod_roster"));
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterResettable_DuplicateIdComparison_IsCaseInsensitive()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                // The core already registered "map_zones" ("quests" comes from
                // the quests_base mod); a differently-cased duplicate must not
                // grow the list.
                services.RegisterResettable(new TestResettable("MAP_ZONES"));

                Assert.Equal(1, services.Resettables.Count);
                Assert.DoesNotContain(services.Resettables, r => r.Id == "MAP_ZONES");
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void ResetForNewGame_IsRepeatable()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var owner = new TestResettable("mod_roster");
                services.RegisterResettable(owner);

                AppServices.Current.ResetForNewGame();
                AppServices.Current.ResetForNewGame();

                Assert.Equal(2, owner.ResetCount);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        /// <summary>
        /// Test double mirroring AppServices' private LambdaResettable: an
        /// owner identified by a stable id, resetting through a delegate.
        /// </summary>
        private sealed class TestResettable : IResettable
        {
            private readonly Action onReset;

            public TestResettable(string id, Action onReset = null)
            {
                Id = id;
                this.onReset = onReset;
            }

            public string Id { get; }

            public int ResetCount { get; private set; }

            public void ResetForNewGame()
            {
                ResetCount++;
                onReset?.Invoke();
            }
        }
    }
}
