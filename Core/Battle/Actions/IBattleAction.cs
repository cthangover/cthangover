namespace Cthangover.Core.Battle.Actions
{
    /// <summary>
    /// A single step in the battle action queue (animation, damage, effect).
    /// DoStart is called once when the action begins, DoAction is polled
    /// every frame — it returns true when the action is done — and DoEnd
    /// is the cleanup callback. This polling model lets actions drive
    /// their own duration (e.g. a tween runs until finished) without
    /// the machine needing timers.
    /// </summary>
    public interface IBattleAction
    {
        /// <summary>
        /// Called once when the action is dequeued and begins execution.
        /// Use for setup: spawning nodes, caching references, starting
        /// tweens or coroutines.
        /// </summary>
        void DoStart();
        /// <summary>
        /// Polled every frame. Returns <c>true</c> when the action has
        /// finished its work; returns <c>false</c> to remain active.
        /// This model eliminates the need for the machine to manage timers.
        /// </summary>
        bool DoAction();
        /// <summary>
        /// Cleanup callback fired after <see cref="DoAction"/> returns
        /// <c>true</c>. Use for teardown: freeing nodes, resetting state,
        /// applying final stat deltas.
        /// </summary>
        void DoEnd();
    }
}
