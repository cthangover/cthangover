using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    public class AttackToEnemyStrategy : ICardActionStrategy
    {
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return action.Type == ActionCharacterType.ToEnemy && target != null && !target.IsPlayer;
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

            var result = ActionExecutorHub.Instance.Execute(action, source.Card, target.Card);
            source.UpdateInfo();
            target.UpdateInfo();

            if (result.Result)
            {
                if (result.Target.Damage > 0)
                    ShowDamageBehaviour.SpawnDamage(result.Target.Damage, target, target.GlobalPosition);

                var defenceLost = -result.Target.Defence;
                if (defenceLost > 0)
                    ShowDamageBehaviour.SpawnDefence(defenceLost, target, target.GlobalPosition);
            }
        }
    }
}
