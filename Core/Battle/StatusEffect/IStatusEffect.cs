using Godot;

namespace Cthangover.Core.Cards.StatusEffect
{

    /// <summary>
    /// Status effect data contract. Separates identity (ID, Name, Icon)
    /// from mutable state (Duration, RemainingTurns) and behaviour
    /// (Actions — an IStatusActions hook set). Copy() is required so the
    /// StatusEffectQueue can clone effects when characters are duplicated.
    /// </summary>
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
