using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Bridge from the battle core back to the Godot scene. Cores work
    /// against this interface so they remain engine-agnostic.
    /// ApplyDamage computes final-damage-minus-defence and mutates
    /// the target's Health directly (no event — the UI layer polls).
    /// ScheduleAnimation is the hook for visual feedback; EndTurn/EndBattle
    /// are phase signals. RootNode gives cores a Godot anchor for
    /// instantiating nodes (e.g. damage numbers).
    /// </summary>
    public interface IBattleContext
    {
        void ApplyDamage(Character source, Character target, int rawDamage, int defence);

        void ScheduleAnimation(IBattleAnimation animation);

        void EndTurn(BattleSide side);

        void EndBattle(BattleSide winner);

        Node RootNode { get; }
    }
}
