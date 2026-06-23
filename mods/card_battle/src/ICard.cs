using Godot;

namespace Cthangover.CardBattle
{

    public interface ICard
    {
        TextureRect Frame { get; }
        TextureRect Image { get; }
    }

}