using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Actions
{

    /// <summary>
    /// Executor that adds a fixed amount of defence to the target character.
    /// Defence acts as a damage-absorbing shield that is consumed before health when the
    /// character takes damage (see <see cref="PhysicsDamageActionCard"/>). The defence delta
    /// comes from <c>ATTRIBUTE_DEFENCE</c> on the action card.
    /// Registered in <see cref="CardBattleActionProvider"/> under <c>"physics/defence"</c>.
    /// </summary>
    public class PhysicsDefenceActionCard : ActionBase
    {

        /// <summary>
        /// Unique identifier registered in <see cref="CardBattleActionProvider"/> as <c>"physics/defence"</c>.
        /// </summary>
        public override string ID => "PhysicsDefenceActionCard";
        
        /// <summary>
        /// Adds a flat defence value to <paramref name="target"/> after deducting action points
        /// from <paramref name="user"/>. The returned <see cref="ChangedAttributes"/> carries the
        /// defence delta for floating text display.
        /// </summary>
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
