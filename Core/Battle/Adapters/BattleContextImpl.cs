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

        /// <summary>
        /// Root Godot <see cref="Node"/> of the battle scene, used by
        /// animation actions to parent spawned visual effects.
        /// </summary>
        public Node RootNode => _sceneContext;

        /// <summary>
        /// The owning <see cref="BattleSceneContext"/> that manages
        /// the battle lifecycle.
        /// </summary>
        public BattleSceneContext SceneContext => _sceneContext;

        /// <summary>
        /// Creates a context adapter around the given scene context.
        /// </summary>
        public BattleContextImpl(BattleSceneContext sceneContext)
        {
            _sceneContext = sceneContext;
        }

        /// <summary>
        /// Subtracts <paramref name="defence"/> from
        /// <paramref name="rawDamage"/>, clamping at 0 to prevent negative
        /// health from defence overflow, and applies the result to
        /// <paramref name="target"/>'s health.
        /// </summary>
        public void ApplyDamage(Character source, Character target, int rawDamage, int defence)
        {
            var finalDamage = Mathf.Max(0, rawDamage - defence);
            target.Attributes.Health.Value -= finalDamage;
        }

        /// <summary>
        /// Stub — specific battle cores override this to enqueue
        /// animation actions into the <see cref="BattleActionMachine"/>.
        /// </summary>
        public void ScheduleAnimation(IBattleAnimation animation)
        {
        }

        /// <summary>
        /// Stub — cores implement this to advance the turn sequence
        /// when a side finishes its action phase.
        /// </summary>
        public void EndTurn(BattleSide side)
        {
        }

        /// <summary>
        /// Resolves the battle outcome by delegating to
        /// <see cref="BattleSceneContext.ShowWinground"/> for player
        /// victory or <see cref="BattleSceneContext.ShowDeadground"/>
        /// for enemy victory.
        /// </summary>
        public void EndBattle(BattleSide winner)
        {
            if (winner == BattleSide.Player)
                _sceneContext.ShowWinground();
            else
                _sceneContext.ShowDeadground();
        }
    }
}
