using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    /// <summary>
    /// Strategy for ally-support actions (<c>ToAlias</c>). Validates that the target is a friendly
    /// player card, highlights with green selection overlays, and applies defence/healing effects
    /// via <see cref="ActionExecutorHub"/>. Allows one player card to buff another during the player's turn.
    /// </summary>
    public class SupportStrategy : ICardActionStrategy
    {
        /// <summary>
        /// Returns <c>true</c> when the action type is <c>ToAlias</c> and the target is a player card.
        /// </summary>
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return action.Type == ActionCharacterType.ToAlias && target != null && target.IsPlayer;
        }

        /// <summary>
        /// Shows a green selection overlay on both the dragged action card and the target ally card.
        /// </summary>
        public void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target)
        {
            actionCard.Select();
            target.Select();
        }

        /// <summary>
        /// Resolves the support action and spawns floating defence and heal text above
        /// the <paramref name="target"/> ally. Damage numbers are shown with the <c>isHeal</c>
        /// flag for green coloring.
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
                if (result.Target.Defence > 0)
                    ShowDamageBehaviour.SpawnDefence(result.Target.Defence, target, target.GlobalPosition);

                if (result.Target.Damage > 0)
                    ShowDamageBehaviour.SpawnDamage(result.Target.Damage, target, target.GlobalPosition, true);
            }
        }
    }
}
