using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.FFBattle.UI;
using Godot;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Abstract base for all FF battle animations. Implements a three-phase lifecycle
    /// (<see cref="DoStart"/>, <see cref="DoAction"/>, <see cref="DoEnd"/>) driven
    /// by the battle core's per-frame loop. Subclasses implement their animation logic
    /// in <c>DoInternalStart/DoInternalAction/DoInternalEnd</c> and are instantiated
    /// by <see cref="FFBattleCore"/> when a player or enemy action is executed.
    /// The <see cref="Speed"/> multiplier scales all timing, used e.g. to speed up
    /// enemy turn animations (<c>0.8f</c>).
    /// </summary>
    public abstract class FFAbstractAnimation : IBattleAction
    {
        /// <summary>Start time of the current animation phase, in seconds (microsecond precision).</summary>
        protected double Timestamp { get; set; } = -1;
        /// <summary>Animation speed multiplier; lower values produce faster animations.</summary>
        protected float Speed { get; set; } = 1f;
        /// <summary>The action descriptor being executed (determines which executor runs).</summary>
        protected ActionCharacter Action { get; set; }
        /// <summary>The character widget performing the action.</summary>
        protected FFCharacterWidget Source { get; set; }
        /// <summary>The character widget receiving the action.</summary>
        protected FFCharacterWidget Target { get; set; }
        /// <summary>World position of <see cref="Source"/> captured at <see cref="DoStart"/>.</summary>
        protected Vector2 SourcePos { get; set; }
        /// <summary>World position of <see cref="Target"/> captured at <see cref="DoStart"/>.</summary>
        protected Vector2 TargetPos { get; set; }

        /// <summary>Creates an animation binding a source, target, and action with an optional speed override.</summary>
        protected FFAbstractAnimation(FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action, float speed = 1f)
        {
            Source = source;
            Target = target;
            Action = action;
            Speed = speed;
        }

        /// <summary>
        /// Advances the animation by one frame. Returns <c>true</c> when the animation
        /// sequence has completed. Called in a tight <c>while</c> loop by
        /// <see cref="FFBattleCore.RunAnimation"/> each process frame.
        /// </summary>
        public bool DoAction()
        {
            if (Source == null || Target == null || Action == null)
                return true;
            return DoInternalAction();
        }

        /// <summary>Captures the initial world positions of source and target, then delegates to subclass setup.</summary>
        public void DoStart()
        {
            if (Source == null || Target == null || Action == null)
                return;

            SourcePos = Source.GlobalPosition;
            TargetPos = Target.GlobalPosition;
            DoInternalStart();
        }

        /// <summary>Restores source to original position and delegates cleanup to subclass.</summary>
        public void DoEnd()
        {
            if (Source == null || Target == null || Action == null)
                return;
            DoInternalEnd();
        }

        protected abstract bool DoInternalAction();
        protected abstract void DoInternalStart();
        protected abstract void DoInternalEnd();

        /// <summary>Quadratic ease-out: fast start, decelerating to the target. Used for return-to-start motion.</summary>
        protected float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        /// <summary>Quadratic ease-in-out: slow start and end, fast in the middle. Used for approach motion.</summary>
        protected float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
}
