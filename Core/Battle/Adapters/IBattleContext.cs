using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.Core.Battle
{
    public interface IBattleContext
    {
        void ApplyDamage(Character source, Character target, int rawDamage, int defence);

        void ScheduleAnimation(IBattleAnimation animation);

        void EndTurn(BattleSide side);

        void EndBattle(BattleSide winner);

        Node RootNode { get; }
    }
}
