using Cthangover.CardBattle.UI;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    /// <summary>
    /// Strategy interface that defines validation, visual highlighting, and execution for
    /// a specific type of card action during the player's drag-and-drop targeting flow.
    /// The <see cref="CardActionStrategyFactory"/> dispatches to the correct strategy based on
    /// the action's <see cref="ActionCharacterType"/>. <see cref="CardController"/> calls
    /// <see cref="Check"/> during drag to validate targets, <see cref="HighlightTarget"/> to show
    /// valid/invalid visual feedback, and <see cref="Execute"/> when the card is dropped on a valid target.
    /// </summary>
    public interface ICardActionStrategy
    {
        /// <summary>
        /// Validates whether <paramref name="action"/> can be performed by <paramref name="source"/>
        /// on <paramref name="target"/>. Called each frame during drag to determine visual feedback.
        /// </summary>
        bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target);
        /// <summary>
        /// Applies visual highlighting to both the <paramref name="actionCard"/> being dragged
        /// and the <paramref name="target"/> character card (e.g. red outline for attack, green for support).
        /// </summary>
        void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target);
        /// <summary>
        /// Executes the action via <see cref="ActionExecutorHub"/> and spawns floating damage/defence text.
        /// Called on successful card drop by <see cref="CardController.OnEndDrag"/>.
        /// </summary>
        void Execute(CharacterCardNode source, CharacterCardNode target, ActionCharacter action);
    }
}
