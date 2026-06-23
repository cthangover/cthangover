using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    public class SupportStrategy : ICardActionStrategy
    {
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return action.Type == ActionCharacterType.ToAlias && target != null && target.IsPlayer;
        }

        public void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target)
        {
            actionCard.Select();
            target.Select();
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
                if (result.Target.Defence > 0)
                    ShowDamageBehaviour.SpawnDefence(result.Target.Defence, target, target.GlobalPosition);

                if (result.Target.Damage > 0)
                    ShowDamageBehaviour.SpawnDamage(result.Target.Damage, target, target.GlobalPosition, true);
            }
        }
    }
}
