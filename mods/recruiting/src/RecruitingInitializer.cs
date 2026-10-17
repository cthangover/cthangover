using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Lifecycle entry point of the recruiting mod. Behaviours and conditions of
    /// all mods are not discovered here: the kernel delivers them through
    /// <see cref="RecruitBehaviourConsumer"/> and <see cref="RecruitConditionConsumer"/>.
    /// <list type="number">
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

        /// <summary>Nothing to do per mod: recruit plugins arrive through the kernel's assembly consumers.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>
        /// Registers the roster reset owner with AppServices (idempotent —
        /// duplicates by Id are ignored) and installs the
        /// <see cref="CharactersDefeatedEvent"/> subscription. The roster is no longer cleared through a
        /// <c>NewGameStartedEvent</c> subscription: the core's reset loop
        /// invokes <see cref="RecruitingReset"/> instead.
        /// </summary>
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new RecruitingReset());
            EnsureDefeatedSubscription();
        }

        private static void EnsureDefeatedSubscription()
        {
            if (_defeatedSubscribed)
                return;

            _defeatedSubscribed = true;
            ModEventBus.Subscribe<CharactersDefeatedEvent>(DefeatedCharactersRecruiter.Process);
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
