using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Actions
{

    /// <summary>
    /// Executor that applies a stun status effect to the target character.
    /// The stun duration (number of turns) is read from <c>ATTRIBUTE_TURN</c> on the action card.
    /// During a stunned character's turn, <see cref="StatusEffectQueue.HasStun"/> returns <c>true</c>,
    /// causing <see cref="CardBattleCore.RunEnemyTurn"/> to skip that character's actions entirely.
    /// Registered in <see cref="CardBattleActionProvider"/> under <c>"physics/stun"</c>.
    /// </summary>
    public class PhysicsStunActionCard : ActionBase
    {

        /// <summary>
        /// Unique identifier registered in <see cref="CardBattleActionProvider"/> as <c>"physics/stun"</c>.
        /// </summary>
        public override string ID => "PhysicsStunActionCard";
        
        /// <summary>
        /// Adds a <see cref="StunStatusEffect"/> with the configured turn count to <paramref name="target"/>'s
        /// <see cref="StatusEffectQueue"/> after deducting action points from <paramref name="user"/>.
        /// </summary>
        public override ChangedAttributes Execute(ActionCharacter actionCard, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(actionCard, user))
                return new ChangedAttributes { Result = false };

            target.StatusEffectQueue.Add("effect/physics/stun", actionCard.GetInt(ActionCharacter.ATTRIBUTE_TURN, 3));
            
            return new ChangedAttributes { Result = true };
        }
    }

}
