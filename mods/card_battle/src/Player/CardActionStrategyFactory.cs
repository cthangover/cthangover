using Cthangover.CardBattle.UI;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    public static class CardActionStrategyFactory
    {
        public static ICardActionStrategy Get(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            if (action == null || source == null || target == null)
                return null;

            switch (action.Type)
            {
                case ActionCharacterType.ToEnemy:
                    return new AttackToEnemyStrategy();
                case ActionCharacterType.ToAlias:
                    return new SupportStrategy();
                case ActionCharacterType.ToSelf:
                    return new SelfStrategy();
                default:
                    return null;
            }
        }

        public static bool CanTarget(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            if (action == null || source == null || target == null)
                return false;

            return Get(action, source, target)?.Check(action, source, target) ?? false;
        }
    }
}
