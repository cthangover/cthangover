using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    /// <summary>
    /// Strategy for <c>ToEnemy</c> attack actions. Validates that the target is an enemy
    /// (non-player card), highlights both cards with a red attack outline, and executes
    /// damage via <see cref="ActionExecutorHub"/> with floating damage/defence numbers.
    /// This is the standard strategy for all offensive cards in the player's deck.
    /// </summary>
    public class AttackToEnemyStrategy : ICardActionStrategy
    {
        /// <summary>
        /// Returns <c>true</c> if the action type is <c>ToEnemy</c> and the target is an enemy card.
        /// </summary>
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return action.Type == ActionCharacterType.ToEnemy && target != null && !target.IsPlayer;
        }

        /// <summary>
        /// Shows a red attack overlay on both the dragged action card and the target enemy card.
        /// </summary>
        public void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target)
        {
            actionCard.Attack();
            target.Attack();
        }

        /// <summary>
        /// Resolves the attack via <see cref="ActionExecutorHub"/> and spawns floating damage
        /// and defence-loss numbers above the <paramref name="target"/> card.
        /// </summary>
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
