using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.CardBattle.Actions
{

    /// <summary>
    /// Executor that applies physical damage from an attack card to a target.
    /// Calculates damage as <c>card_attack_multiplier × user_attack_stat</c>, reduces it by
    /// the target's current defence value (which is consumed in the process), and applies the
    /// remainder to the target's health. Invokes <c>OnTakeDamage</c>/<c>OnDealDamage</c> hooks
    /// on both characters' <see cref="StatusEffectQueue"/> so that damage-modifying status effects
    /// can intercept and adjust the values before they are applied.
    /// Registered in <see cref="CardBattleActionProvider"/> under <c>"physics/attack"</c>.
    /// </summary>
    public class PhysicsDamageActionCard : ActionBase
    {

        /// <summary>
        /// Unique identifier registered in <see cref="CardBattleActionProvider"/> as <c>"physics/attack"</c>.
        /// </summary>
        public override string ID => "PhysicsDamageActionCard";
        
        /// <summary>
        /// Resolves the attack: deducts action points from <paramref name="user"/> via <c>CheckRequiredAndUsePoint</c>,
        /// computes raw damage from the card's attack multiplier and the user's attack stat,
        /// applies status-effect damage hooks, consumes <paramref name="target"/>'s defence shield first,
        /// then subtracts remaining damage from health. Returns a <see cref="ChangedAttributes"/> with
        /// the actual damage and defence-loss deltas for the floating text display.
        /// </summary>
        public override ChangedAttributes Execute(ActionCharacter actionCard, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(actionCard, user))
                return new ChangedAttributes { Result = false };

            var damageDelta = Mathf.RoundToInt(actionCard.GetFloat(ActionCharacter.ATTRIBUTE_ATTACK, 1f) * user.Attributes.Attack.Value);
            target.StatusEffectQueue.OnTakeDamage(user, ref damageDelta);
            user.StatusEffectQueue.OnDealDamage(target, ref damageDelta);
            
            var defenceDelta = 0;
            if (target.Attributes.Defence.Value > 0)
            {
                defenceDelta = target.Attributes.Defence.Value >= damageDelta ? damageDelta : target.Attributes.Defence.Value;
                damageDelta  -= defenceDelta;
            }
            target.Attributes.Defence.Value -= defenceDelta;
            target.Attributes.Health.Value  -= damageDelta;
            
            return new ChangedAttributes { Result = true, Target = { Damage = damageDelta, Defence = -defenceDelta}};
        }

    }

}
