using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Actions
{

    public class PhysicsStunActionCard : ActionBase
    {

        public override string ID => "PhysicsStunActionCard";
        
        public override ChangedAttributes Execute(ActionCharacter actionCard, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(actionCard, user))
                return new ChangedAttributes { Result = false };

            target.StatusEffectQueue.Add("effect/physics/stun", actionCard.GetInt(ActionCharacter.ATTRIBUTE_TURN, 3));
            
            return new ChangedAttributes { Result = true };
        }
    }

}
