namespace Cthangover.CardBattle.Actions
{
    /// <summary>
    /// Phases of the animated battle action state machine used by <see cref="PhysicsAttackAction"/>
    /// and <see cref="PhysicsDefenceAction"/>. Each tick, the action evaluates the current phase,
    /// interpolates card positions/rotations/scales, and transitions to the next phase when its
    /// duration elapses. The <c>Recover</c> phase signals completion by returning <c>true</c> from <c>DoInternalAction</c>.
    /// </summary>
    public enum ActionPhase
    {
        /// <summary>Initial delay before any movement.</summary>
        Wait,
        /// <summary>Source card rotates toward target and begins slight movement.</summary>
        Prepare,
        /// <summary>Source card travels from its position to the target along a movement pattern.</summary>
        MoveForAttack,
        /// <summary>Damage/effect is applied to the target card at the point of impact.</summary>
        AttackImpact,
        /// <summary>Target card shows knockback, squashing, and shaking reaction.</summary>
        TargetReaction,
        /// <summary>Source card returns toward its original position.</summary>
        MoveForBack,
        /// <summary>All cards smoothly return to their original transforms.</summary>
        Recover
    }
}
