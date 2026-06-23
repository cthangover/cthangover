using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.FFBattle.UI;
using Godot;

namespace Cthangover.FFBattle.Actions
{
    public class FFAttackAnimation : FFAbstractAnimation
    {
        private enum Phase { MoveForward, Impact, MoveBack, Done }

        private Phase _phase;
        private bool _damageApplied;
        private Vector2 _midPoint;

        public FFAttackAnimation(FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action, float speed = 1f)
            : base(source, target, action, speed) { }

        protected override void DoInternalStart()
        {
            Timestamp = Time.GetTicksUsec() / 1_000_000.0;
            _damageApplied = false;
            _phase = Phase.MoveForward;

            var dir = (TargetPos - SourcePos).Normalized();
            _midPoint = TargetPos - dir * 30f;
        }

        protected override bool DoInternalAction()
        {
            var elapsed = (float)(Time.GetTicksUsec() / 1_000_000.0 - Timestamp) * Speed * (float)Engine.TimeScale;

            switch (_phase)
            {
                case Phase.MoveForward:
                {
                    var progress = Mathf.Clamp(elapsed / 0.35f, 0f, 1f);
                    Source.GlobalPosition = SourcePos.Lerp(_midPoint, EaseInOutQuad(progress));

                    if (progress >= 1f)
                    {
                        _phase = Phase.Impact;
                        Timestamp = Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.Impact:
                {
                    var progress = Mathf.Clamp(elapsed / 0.2f, 0f, 1f);

                    if (!_damageApplied && progress >= 0.3f)
                    {
                        _damageApplied = true;
                        var result = ActionExecutorHub.Instance.Execute(Action, Source.Card, Target.Card);
                        Source.UpdateInfo();
                        Target.UpdateInfo();

                        if (result.Result)
                        {
                            Target.Flash(new Color(1f, 0.3f, 0.3f, 1f), 0.2f);
                            Target.Shake(4f, 0.25f);

                            if (result.Target.Damage > 0)
                                ShowDamageBehaviour.SpawnDamage(result.Target.Damage, Target, Target.GlobalPosition);

                            var defenceLost = -result.Target.Defence;
                            if (defenceLost > 0)
                                ShowDamageBehaviour.SpawnDefence(defenceLost, Target, Target.GlobalPosition);
                        }
                    }

                    if (progress >= 1f)
                    {
                        _phase = Phase.MoveBack;
                        Timestamp = Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.MoveBack:
                {
                    var progress = Mathf.Clamp(elapsed / 0.4f, 0f, 1f);
                    Source.GlobalPosition = _midPoint.Lerp(SourcePos, EaseOutQuad(progress));

                    if (progress >= 1f)
                    {
                        _phase = Phase.Done;
                        return true;
                    }
                    break;
                }
            }

            return false;
        }

        protected override void DoInternalEnd()
        {
            Source.GlobalPosition = SourcePos;
            Source.UpdateInfo();
            Target.UpdateInfo();
        }
    }
}
