using Cthangover.CardBattle.UI;
using Cthangover.Core.Characters;

namespace Cthangover.CardBattle.Player
{
    public interface ICardActionStrategy
    {
        bool Check(ActionCharacter action, CharacterCardNode source, CharacterCardNode target);
        void HighlightTarget(ActionCardNode actionCard, CharacterCardNode target);
        void Execute(CharacterCardNode source, CharacterCardNode target, ActionCharacter action);
    }
}
