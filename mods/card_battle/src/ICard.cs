using Godot;

namespace Cthangover.CardBattle
{

    /// <summary>
    /// Common interface for all card UI nodes in the card battle system.
    /// Exposes the <c>Frame</c> and <c>Image</c> <see cref="TextureRect"/> controls so that
    /// <see cref="CardController"/> can perform hit-testing during drag-and-drop targeting
    /// without knowing the concrete card type (<see cref="CharacterCardNode"/> or <see cref="ActionCardNode"/>).
    /// </summary>
    public interface ICard
    {
        /// <summary>
        /// The frame <see cref="TextureRect"/> used for hit-testing whether a pointer is over this card.
        /// </summary>
        TextureRect Frame { get; }
        /// <summary>
        /// The portrait/image <see cref="TextureRect"/> of the card, used for dissolve shader effects on death.
        /// </summary>
        TextureRect Image { get; }
    }

}