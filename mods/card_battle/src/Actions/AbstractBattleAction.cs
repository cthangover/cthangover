using Cthangover.CardBattle.UI;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Godot;

namespace Cthangover.CardBattle.Actions
{
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

        public void DoEnd()
        {
            if (Source == null || Target == null || Action == null)
                return;
            DoInternalEnd();
        }
    }
}
