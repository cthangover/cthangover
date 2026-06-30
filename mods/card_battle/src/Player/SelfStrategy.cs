using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    /// <summary>
    /// Strategy for self-targeting actions (<c>ToSelf</c>). Validates that source and target
    /// are the same card, highlights with a green selection outline, and applies the action
    /// to the source card itself via <see cref="ActionExecutorHub"/>. Used for self-buffs like
    /// defence boosts. Damage and defence floating text spawn above the source card.
    /// </summary>
    public class SelfStrategy : ICardActionStrategy
    {
        /// <summary>
        /// Returns <c>true</c> only when <paramref name="source"/> and <paramref name="target"/> are the same card.
        /// </summary>
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return source != null && source == target;
        }

        /// <summary>
        /// Shows a green selection overlay on both the dragged action card and the source card.
        /// </summary>
        public void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target)
        {
            actionCard.Select();
            target.Select();
        }

        /// <summary>
        /// Executes the self-targeted action and spawns floating defence/damage numbers
        /// above the <paramref name="source"/> card. Damage numbers are marked with the
        /// <c>isHeal</c> flag for green coloring via <see cref="ShowDamageBehaviour"/>.
        /// </summary>
        public void Execute(CharacterCardNode source, CharacterCardNode target, ActionCharacter action)
        {
            if (source?.Card == null || action == null)
                return;

            var result = ActionExecutorHub.Instance.Execute(action, source.Card, source.Card);
            source.UpdateInfo();

            if (result.Result)
            {
                if (result.Target.Defence > 0)
                    ShowDamageBehaviour.SpawnDefence(result.Target.Defence, source, source.GlobalPosition);

                if (result.Target.Damage > 0)
                    ShowDamageBehaviour.SpawnDamage(result.Target.Damage, source, source.GlobalPosition, true);
            }
        }
    }
}
