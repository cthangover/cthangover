using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.FFBattle.UI;
using Godot;

namespace Cthangover.FFBattle.Actions
{
    public abstract class FFAbstractAnimation : IBattleAction
    {
        protected double Timestamp { get; set; } = -1;
        protected float Speed { get; set; } = 1f;
        protected ActionCharacter Action { get; set; }
        protected FFCharacterWidget Source { get; set; }
        protected FFCharacterWidget Target { get; set; }
        protected Vector2 SourcePos { get; set; }
        protected Vector2 TargetPos { get; set; }

        protected FFAbstractAnimation(FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action, float speed = 1f)
        {
            Source = source;
            Target = target;
            Action = action;
            Speed = speed;
        }

        public bool DoAction()
        {
            if (Source == null || Target == null || Action == null)
                return true;
            return DoInternalAction();
        }

        public void DoStart()
        {
            if (Source == null || Target == null || Action == null)
                return;

            SourcePos = Source.GlobalPosition;
            TargetPos = Target.GlobalPosition;
            DoInternalStart();
        }

        public void DoEnd()
        {
            if (Source == null || Target == null || Action == null)
                return;
            DoInternalEnd();
        }

        protected abstract bool DoInternalAction();
        protected abstract void DoInternalStart();
        protected abstract void DoInternalEnd();

        protected float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        protected float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
}
