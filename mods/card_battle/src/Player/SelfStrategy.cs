using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    public class SelfStrategy : ICardActionStrategy
    {
        public bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            return source != null && source == target;
        }

        public void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target)
        {
            actionCard.Select();
            target.Select();
        }

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
