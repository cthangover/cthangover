using Godot;

namespace Cthangover.Core.Cards.StatusEffect
{

    public interface IStatusEffect
    {
        string ID { get; }
        string Name { get; }
        string Description { get; }
        StatusEffectType EffectType { get; }
        int Duration { get; set; }
        int RemainingTurns { get; set; }
        Texture2D Icon { get; }
        IStatusActions Actions { get; }

        IStatusEffect Copy();
    }
}
