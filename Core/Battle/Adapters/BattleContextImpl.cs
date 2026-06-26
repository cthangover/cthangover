using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Default IBattleContext wired to the current BattleSceneContext.
    /// ApplyDamage clamps at 0 (no negative health from defence overflow),
    /// EndBattle delegates to ShowWinground/ShowDeadground on the scene
    /// context. ScheduleAnimation and EndTurn are stubs — specific battle
    /// cores are expected to override or supplement this behaviour.
    /// </summary>
    public class BattleContextImpl : IBattleContext
    {
        private readonly BattleSceneContext _sceneContext;

        public Node RootNode => _sceneContext;

        public BattleSceneContext SceneContext => _sceneContext;

        public BattleContextImpl(BattleSceneContext sceneContext)
        {
            _sceneContext = sceneContext;
        }

        public void ApplyDamage(Character source, Character target, int rawDamage, int defence)
        {
            var finalDamage = Mathf.Max(0, rawDamage - defence);
            target.Attributes.Health.Value -= finalDamage;
        }

        public void ScheduleAnimation(IBattleAnimation animation)
        {
        }

        public void EndTurn(BattleSide side)
        {
        }

        public void EndBattle(BattleSide winner)
        {
            if (winner == BattleSide.Player)
                _sceneContext.ShowWinground();
            else
                _sceneContext.ShowDeadground();
        }
    }
}
