using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.CardBattle.Actions
{

    public class PhysicsDamageActionCard : ActionBase
    {

        public override string ID => "PhysicsDamageActionCard";
        
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
