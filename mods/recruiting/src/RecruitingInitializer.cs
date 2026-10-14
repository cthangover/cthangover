using System;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Bootstrap;
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
    /// <item>It registers a <see cref="RecruitingReset"/> with
    /// <see cref="AppServices"/>, so the core's "New Game" pipeline
    /// (<c>GameData.StartNewGame</c> → <c>AppServices.ResetForNewGame</c>)
    /// clears the roster — the core used to discard it implicitly with
    /// RuntimeData, but the roster now lives in
    /// <see cref="RecruitingData.Instance"/> and must be reset explicitly on
    /// "New Game". Registration is idempotent: AppServices deduplicates by
    /// <c>IResettable.Id</c>, so a repeated <c>OnModResourcesReady</c> is safe.</item>
    /// <item>It subscribes to <see cref="CharactersDefeatedEvent"/> and routes it
    /// into <see cref="DefeatedCharactersRecruiter"/> — the publisher (battle,
    /// trap, script) stays unaware of recruiting, so the whole system can be
    /// disabled by removing this mod.</item>
    /// </list>
    /// Persistence is not handled here: <see cref="RecruitingSaveable"/> is an
    /// <c>IModSaveable</c> and is discovered by ModSaveableRegistry automatically.
    /// </summary>
    public class RecruitingInitializer : IModInitializer
    {
        private static bool _defeatedSubscribed;

        /// <summary>Registers behaviours and conditions from the mod that just announced itself.</summary>
        public void OnModLoaded(string modId)
        {
            RegisterAssembly(FindAssembly(modId));
        }

        /// <summary>
        /// Registers the roster reset owner with AppServices (idempotent —
        /// duplicates by Id are ignored), installs the
        /// <see cref="CharactersDefeatedEvent"/> subscription, and rescans
        /// every loaded assembly. The roster is no longer cleared through a
        /// <c>NewGameStartedEvent</c> subscription: the core's reset loop
        /// invokes <see cref="RecruitingReset"/> instead.
        /// </summary>
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new RecruitingReset());
            EnsureDefeatedSubscription();
            RecruitBehaviourRegistry.Instance.RegisterAll();
        }

        private static void EnsureDefeatedSubscription()
        {
            if (_defeatedSubscribed)
                return;

            _defeatedSubscribed = true;
            ModEventBus.Subscribe<CharactersDefeatedEvent>(DefeatedCharactersRecruiter.Process);
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

    /// <summary>
    /// New-game state owner of the recruiting mod: clears the session roster
    /// (<see cref="RecruitingData.Data"/>). Registered from
    /// <see cref="RecruitingInitializer.OnModResourcesReady"/>;
    /// <see cref="AppServices"/> deduplicates by Id, so repeated registration
    /// is a no-op. The clear is a plain list clear with no per-recruit side
    /// effects — unlike <see cref="RecruitingData.Remove"/> it does not notify
    /// <c>RecruitBehaviourRegistry.OnRemove</c> or detach companions through
    /// <c>RecruitPartyBridge</c> — matching the previous
    /// <c>NewGameStartedEvent</c> handler's behavior. Save loading is a
    /// separate path: <c>RecruitingSaveable</c> replaces the roster contents
    /// when a save is loaded.
    /// </summary>
    internal sealed class RecruitingReset : IResettable
    {
        /// <summary>Resettable key under which AppServices deduplicates registration.</summary>
        public string Id => "recruiting";

        /// <summary>Clears the roster wholesale on "New Game", without per-recruit notifications.</summary>
        public void ResetForNewGame() => RecruitingData.Instance.Data.Clear();
    }
}
