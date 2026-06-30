using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    /// <summary>
    /// Strategy for stun actions targeting enemies. Extends the standard <c>ToEnemy</c> check
    /// by also verifying that the action has at least 2 turns of stun duration
    /// (<c>ATTRIBUTE_TURN > 1</c>). Highlights with red attack overlays and applies
    /// the stun effect via <see cref="ActionExecutorHub"/> without spawning damage text
    /// (stun has no numerical effect to display).
    /// Not currently returned by <see cref="CardActionStrategyFactory"/> — available for
    /// future use when stun cards need distinct drag-and-drop behavior from attack cards.
    /// </summary>
    public class StunToEnemyStrategy : ICardActionStrategy
    {
        /// <summary>
        /// Returns <c>true</c> when the action targets an enemy and has a turn count greater than 1.
        /// </summary>
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return action.Type == ActionCharacterType.ToEnemy && target != null && !target.IsPlayer
                && action.GetInt(ActionCharacter.ATTRIBUTE_TURN, 0) > 1;
        }

        /// <summary>
        /// Shows a red attack overlay on both cards, same as <see cref="AttackToEnemyStrategy"/>.
        /// </summary>
        public void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target)
        {
            actionCard.Attack();
            target.Attack();
        }

        /// <summary>
        /// Applies the stun via <see cref="ActionExecutorHub"/>. Does not spawn damage/defence text.
        /// </summary>
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
