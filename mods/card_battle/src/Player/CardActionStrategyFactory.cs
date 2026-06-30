using Cthangover.CardBattle.UI;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    /// <summary>
    /// Factory that returns the appropriate <see cref="ICardActionStrategy"/> implementation
    /// for a given <see cref="ActionCharacter"/>. Dispatches based on <see cref="ActionCharacterType"/>:
    /// <c>ToEnemy</c> → <see cref="AttackToEnemyStrategy"/>,
    /// <c>ToAlias</c> → <see cref="SupportStrategy"/>,
    /// <c>ToSelf</c> → <see cref="SelfStrategy"/>.
    /// Used by <see cref="CardController"/> during drag-and-drop to determine valid targeting behavior
    /// and by <see cref="CanTarget"/> for pre-validation.
    /// </summary>
    public static class CardActionStrategyFactory
    {
        /// <summary>
        /// Returns the matching strategy for the action's type, or <c>null</c> if any argument is null
        /// or the type is unrecognized.
        /// </summary>
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

        /// <summary>
        /// Convenience method that gets the strategy via <see cref="Get"/> and immediately calls
        /// <see cref="ICardActionStrategy.Check"/> on it. Returns <c>false</c> if no strategy matches
        /// or any argument is null.
        /// </summary>
        public static bool CanTarget(ActionCharacter action, CharacterCardNode source, CharacterCardNode target)
        {
            if (action == null || source == null || target == null)
                return false;

            return Get(action, source, target)?.Check(action, source, target) ?? false;
        }
    }
}
