using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.CardBattle.Actions
{
    /// <summary>
    /// Base class for animated battle actions that move, rotate, and scale card controls
    /// through a sequence of visual phases. Subclasses implement <c>DoInternalAction</c> to advance
    /// a frame-by-frame animation state machine driven by <see cref="CardBattleCore"/>'s
    /// per-frame polling loop (<c>while (!DoAction()) await frame;</c>).
    /// Captures source/target positions and rotations in <see cref="DoStart"/> so that
    /// <see cref="DoEnd"/> can restore them regardless of what the animation did.
    /// </summary>
    public abstract class AbstractBattleAction : IBattleAction
    {
        protected double Timestamp { get; set; } = -1;
        protected float Speed { get; set; }
        protected ActionCharacter Action { get; set; }
        protected CharacterCardNode Source { get; set; }
        protected CharacterCardNode Target { get; set; }
        protected Control SourceControl { get; set; }
        protected Vector2 SourcePos { get; set; }
        protected float SourceRot { get; set; }
        protected Control TargetControl { get; set; }
        protected Vector2 TargetPos { get; set; }
        protected float TargetRot { get; set; }

        /// <summary>
        /// Advances the animation by one frame. Called in a loop by <see cref="CardBattleCore.RunEnemyTurn"/>
        /// with <c>await root.ToSignal(frame)</c> between calls. Returns <c>true</c> when the animation
        /// has completed all phases and the action result has been applied.
        /// Returns <c>true</c> early if any required node became null (card was destroyed mid-animation).
        /// </summary>
        /// <returns><c>true</c> when the animation is finished or cannot proceed.</returns>
        public bool DoAction()
        {
            if (Source == null || Target == null || Action == null || SourceControl == null || TargetControl == null)
                return true;
            return DoInternalAction();
        }

        protected abstract bool DoInternalAction();
        protected abstract void DoInternalStart();
        protected abstract void DoInternalEnd();

        protected AbstractBattleAction(CharacterCardNode source, CharacterCardNode target, ActionCharacter action, float speed = 1)
        {
            Source = source;
            Target = target;
            Action = action;
            Speed = speed;
        }

        /// <summary>
        /// Captures the current positions, rotations, and <see cref="Control"/> references of both source
        /// and target cards before the animation begins, then delegates to the subclass
        /// <see cref="DoInternalStart"/> for any additional setup (e.g. resetting timestamps).
        /// </summary>
        public void DoStart()
        {
            if (Source == null || Target == null || Action == null)
                return;

            SourceControl = Source.GetControlNode();
            SourcePos = SourceControl.GlobalPosition;
            SourceRot = SourceControl.Rotation;
            TargetControl = Target.GetControlNode();
            TargetPos = TargetControl.GlobalPosition;
            TargetRot = TargetControl.Rotation;

            DoInternalStart();
        }

        /// <summary>
        /// Restores source and target card controls to their original positions, rotations, and visual state
        /// captured during <see cref="DoStart"/>. Delegates specific cleanup to <see cref="DoInternalEnd"/>.
        /// </summary>
        public void DoEnd()
        {
            if (Source == null || Target == null || Action == null)
                return;
            DoInternalEnd();
        }
    }
}
