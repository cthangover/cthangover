using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.FFBattle.UI;
using Godot;

namespace Cthangover.FFBattle.Actions
{
    public class FFDefendAnimation : FFAbstractAnimation
    {
        private enum Phase { Bounce, Glow, Recover, Done }

        private Phase _phase;
        private bool _effectApplied;
        private Vector2 _bounceOffset;

        public FFDefendAnimation(FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action, float speed = 1f)
            : base(source, target, action, speed) { }

        protected override void DoInternalStart()
        {
            Timestamp = Time.GetTicksUsec() / 1_000_000.0;
            _effectApplied = false;
            _phase = Phase.Bounce;
            _bounceOffset = new Vector2(0, -12f);
        }

        protected override bool DoInternalAction()
        {
            var elapsed = (float)(Time.GetTicksUsec() / 1_000_000.0 - Timestamp) * Speed * (float)Engine.TimeScale;

            switch (_phase)
            {
                case Phase.Bounce:
                {
                    var progress = Mathf.Clamp(elapsed / 0.25f, 0f, 1f);
                    var bounce = Mathf.Sin(progress * Mathf.Pi * 2f) * 8f;
                    Source.GlobalPosition = SourcePos + new Vector2(0, -Mathf.Abs(bounce));

                    if (progress >= 1f)
                    {
                        _phase = Phase.Glow;
                        Timestamp = Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.Glow:
                {
                    var progress = Mathf.Clamp(elapsed / 0.4f, 0f, 1f);

                    if (!_effectApplied && progress >= 0.3f)
                    {
                        _effectApplied = true;
                        var result = ActionExecutorHub.Instance.Execute(Action, Source.Card, Target.Card);
                        Source.UpdateInfo();
                        Target.UpdateInfo();

                        if (result.Result)
                        {
                            Target.Flash(new Color(0.3f, 1f, 0.3f, 1f), 0.4f);

                            if (result.Target.Defence > 0)
                                ShowDamageBehaviour.SpawnDefence(result.Target.Defence, Target, Target.GlobalPosition);

                            if (result.Target.Damage > 0)
                                ShowDamageBehaviour.SpawnDamage(result.Target.Damage, Target, Target.GlobalPosition, true);
                        }
                    }

                    if (progress >= 1f)
                    {
                        _phase = Phase.Recover;
                        Timestamp = Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.Recover:
                {
                    var progress = Mathf.Clamp(elapsed / 0.3f, 0f, 1f);
                    Source.GlobalPosition = (SourcePos + _bounceOffset).Lerp(SourcePos, EaseOutQuad(progress));

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
