using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.StatusEffect
{
    /// <summary>
    /// Status effect that causes a character to skip their entire turn.
    /// On <see cref="OnTurnStart"/>, calls <see cref="IStatusActions.SkipTurn"/> which sets
    /// <see cref="StatusEffectQueue.SkipTurn"/> to <c>true</c>. <see cref="CardBattleCore.RunEnemyTurn"/>
    /// checks this flag and skips the character's action selection and resolution entirely.
    /// Created by <see cref="PhysicsStunActionCard"/> when a stun card is played,
    /// and added to the target's <see cref="StatusEffectQueue"/> via <c>Add("effect/physics/stun", turns)</c>.
    /// </summary>
    public class StunStatusEffect : IStatusEffect
    {
        /// <summary>Type identifier for stun (1).</summary>
        public int Type => 1;
        /// <summary>Number of turns this stun remains active.</summary>
        public int Turns { get; set; }
        /// <summary>Expired when the turn counter reaches zero or below.</summary>
        public bool IsExpired => Turns <= 0;

        /// <summary>
        /// Creates a stun effect lasting for <paramref name="turns"/> turns.
        /// </summary>
        public StunStatusEffect(int turns)
        {
            Turns = turns;
        }

        /// <summary>
        /// Parameterless constructor for deserialization. Sets <see cref="Turns"/> to 0 (expired immediately).
        /// </summary>
        public StunStatusEffect()
        {
        }

        /// <summary>
        /// Calls <see cref="IStatusActions.SkipTurn"/> to mark the character's turn as skipped.
        /// </summary>
        public void OnTurnStart(Character character, IStatusActions actions)
        {
            actions.SkipTurn();
        }

        /// <summary>
        /// Stun has no end-of-turn effect.
        /// </summary>
        public void OnTurnEnd(Character character, IStatusActions actions)
        {
        }
    }
}
