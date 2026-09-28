using System;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Lifecycle entry point of the recruiting mod. The core's ModAssemblyLoader
    /// no longer knows about recruit behaviours, so this initializer takes over:
    /// <list type="number">
    /// <item>Every mod announcement triggers a scan of that mod's assembly for
    /// <see cref="IRecruitBehaviour"/> and <see cref="IRecruitCondition"/>
    /// implementations (the compiler names source-compiled assemblies after their
    /// mod id).</item>
    /// <item>The resources-ready pass rescans the whole AppDomain as a safety net
    /// for precompiled DLLs whose assembly name differs from the mod id.</item>
    /// <item>It subscribes to <see cref="CharactersDefeatedEvent"/> and routes it
    /// into <see cref="DefeatedCharactersRecruiter"/> — the publisher (battle,
    /// trap, script) stays unaware of recruiting, so the whole system can be
    /// disabled by removing this mod.</item>
    /// <item>It also subscribes to <see cref="NewGameStartedEvent"/> and clears
    /// the roster — the core used to discard it implicitly with RuntimeData, but
    /// the roster now lives in <see cref="RecruitingData.Instance"/> and must be
    /// reset explicitly on "New Game".</item>
    /// </list>
    /// Persistence is not handled here: <see cref="RecruitingSaveable"/> is an
    /// <c>IModSaveable</c> and is discovered by ModSaveableRegistry automatically.
    /// </summary>
    public class RecruitingInitializer : IModInitializer
    {
        private static bool _newGameSubscribed;
        private static bool _defeatedSubscribed;

        /// <summary>Registers behaviours and conditions from the mod that just announced itself.</summary>
        public void OnModLoaded(string modId)
        {
            RegisterAssembly(FindAssembly(modId));
        }

        /// <summary>Installs the event subscriptions and rescans every loaded assembly.</summary>
        public void OnModResourcesReady()
        {
            EnsureNewGameReset();
            EnsureDefeatedSubscription();
            RecruitBehaviourRegistry.Instance.RegisterAll();
        }

        private static void EnsureNewGameReset()
        {
            if (_newGameSubscribed)
                return;

            _newGameSubscribed = true;
            ModEventBus.Subscribe<NewGameStartedEvent>(OnNewGameStarted);
        }

        private static void EnsureDefeatedSubscription()
        {
            if (_defeatedSubscribed)
                return;

            _defeatedSubscribed = true;
            ModEventBus.Subscribe<CharactersDefeatedEvent>(DefeatedCharactersRecruiter.Process);
        }

        private static void OnNewGameStarted(NewGameStartedEvent evt)
        {
            RecruitingData.Instance.Data.Clear();
        }

        private static void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null)
                return;

            RecruitBehaviourRegistry.Instance.RegisterAssembly(assembly);
        }

        private static Assembly FindAssembly(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return null;

            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == modId);
        }
    }
}
