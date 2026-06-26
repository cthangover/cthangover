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
        void DoStart();
        bool DoAction();
        void DoEnd();
    }
}
