using Cthangover.Core.Characters;
using Cthangover.Core.Events;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Turns a <see cref="CharactersDefeatedEvent"/> into recruit attempts. The
    /// recruiting mod subscribes to the core-neutral event, so the system that
    /// defeated the characters (battle, trap, script) does not know about
    /// recruiting — and recruiting does not depend on any combat mod. This
    /// replaces the old hardcoded call inside the victory screen.
    /// <para/>
    /// For every reported id the character template is resolved through
    /// <see cref="CharacterFactory"/> (the battle copy and the template share
    /// <c>RecruitmentChance</c>), skipped when that enemy is already captured
    /// (only one recruit per enemy type is allowed), checked against the
    /// condition gate (<see cref="RecruitBehaviourRegistry.CanRecruit"/>),
    /// rolled 1–100 and, on success, added to the roster. The roll is skipped
    /// entirely when no conditions are registered or the chance is zero,
    /// matching the old flow.
    /// <para/>
    /// The event also marks the end of a fight for the recruiting mod, so the
    /// final health of every squad recruit is pulled back from its placeholder
    /// card before new recruits are resolved.
    /// </summary>
    public static class DefeatedCharactersRecruiter
    {
        /// <summary>Event-bus handler; safe to call with a null or empty payload.</summary>
        public static void Process(CharactersDefeatedEvent evt)
        {
            if (evt?.CharacterIds == null)
                return;

            var runtime = GameData.Instance?.Runtime;
            if (runtime == null)
                return;

            var registry = RecruitBehaviourRegistry.Instance;

            // A defeat resolves the battle: pull the final health of every
            // squad recruit back from its placeholder card, since battle writes
            // into the shared attributes object while the mod keeps its own data.
            foreach (var recruit in RecruitingData.Instance.Data)
                RecruitPartyBridge.SyncFromCard(recruit);

            foreach (var id in evt.CharacterIds)
            {
                if (string.IsNullOrEmpty(id))
                    continue;

                var enemy = CharacterFactory.Instance.Get(id);
                if (enemy == null || enemy.RecruitmentChance <= 0)
                    continue;

                if (RecruitingData.Instance.HasCharacterID(enemy.ID))
                    continue;

                if (!registry.CanRecruit(enemy, runtime))
                    continue;

                if (System.Random.Shared.Next(1, 101) > enemy.RecruitmentChance)
                    continue;

                RecruitingData.Instance.Add(enemy.ID, enemy.ID);
                GameLogger.Log("RECRUIT",
                    $"Recruited '{enemy.ID}' after '{evt.Source}' defeat", LogLevel.Message);
            }
        }
    }
}
