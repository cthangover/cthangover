using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    public class StunToEnemyStrategy : ICardActionStrategy
    {
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return action.Type == ActionCharacterType.ToEnemy && target != null && !target.IsPlayer
                && action.GetInt(ActionCharacter.ATTRIBUTE_TURN, 0) > 1;
        }

        public void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target)
        {
            actionCard.Attack();
            target.Attack();
        }

        public void Execute(CharacterCardNode source, CharacterCardNode target, ActionCharacter action)
        {
            if (source?.Card == null || target?.Card == null || action == null)
                return;

            ActionExecutorHub.Instance.Execute(action, source.Card, target.Card);
            source.UpdateInfo();
            target.UpdateInfo();
        }
    }
}
