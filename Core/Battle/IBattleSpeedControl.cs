namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Minimal contract between <see cref="BattleSceneContext"/> and the
    /// battle view's speed control. The concrete x1/x4/x8 widget lives in the
    /// turn-battle GUI mod, so the core scene only needs the one call it makes
    /// when a battle ends — resetting the global time scale without persisting it.
    /// </summary>
    public interface IBattleSpeedControl
    {
        /// <summary>
        /// Restores <c>Engine.TimeScale</c> to 1x without saving the preference.
        /// Called when the battle ends so menus and the lobby run at normal speed
        /// regardless of the player's in-battle speed choice.
        /// </summary>
        void ResetToNormal();
    }
}
