using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Actions
{

    public class PhysicsDefenceActionCard : ActionBase
    {

        public override string ID => "PhysicsDefenceActionCard";
        
        public override ChangedAttributes Execute(ActionCharacter actionCard, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(actionCard, user))
                return new ChangedAttributes { Result = false };

            var defenceDelta = actionCard.GetInt(ActionCharacter.ATTRIBUTE_DEFENCE);
            target.Attributes.Defence.Value += defenceDelta;
            
            return new ChangedAttributes { Result = true, Target = { Defence = defenceDelta}};
        }

    }

}
