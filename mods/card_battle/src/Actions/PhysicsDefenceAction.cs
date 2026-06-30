using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.CardBattle.UI;
using Godot;

namespace Cthangover.CardBattle.Actions
{
    /// <summary>
    /// Animated battle action that plays a self-buff or ally-support animation: the source card
    /// briefly rotates toward the target, applies a defence boost via <see cref="ActionExecutorHub"/>,
    /// and shows a subtle block/stagger reaction on the target. Used for both <c>ToSelf</c> and
    /// <c>ToAlias</c> action types. This is the visual counterpart of <see cref="PhysicsDefenceActionCard"/>.
    /// Created by <see cref="CardBattleCore.CreateAnimatedAction"/> for enemy self-buff/support turns.
    /// </summary>
    public class PhysicsDefenceAction : AbstractBattleAction
    {
        private ActionPhase phase = ActionPhase.Prepare;
        private float defendRotation;
        private bool effectApplied;

        private Vector2 targetOriginalScale;
        private Color targetOriginalColor;

        private Vector2 blockScale;
        private Vector2 blockKnockback;
        private Color blockColor;

        private float prepareDuration = 0.3f;
        private float attackImpactDuration = 0.4f;
        private float targetReactionDuration = 0.3f;
        private float moveForBackDuration = 0.4f;
        private float recoverDuration = 0.3f;

        /// <summary>
        /// Creates a defence/support animation. Both <paramref name="source"/> and <paramref name="target"/>
        /// can be the same card (for self-buffs) or different (for ally support).
        /// <paramref name="speed"/> multiplies all phase durations.
        /// </summary>
        public PhysicsDefenceAction(CharacterCardNode source, CharacterCardNode target, ActionCharacter action, float speed = 1)
            : base(source, target, action, speed) { }

        protected override void DoInternalStart()
        {
            Timestamp = Time.GetTicksUsec() / 1_000_000.0;
            effectApplied = false;
            phase = ActionPhase.Prepare;
            targetOriginalScale = TargetControl.Scale;
            targetOriginalColor = TargetControl.Modulate;
        }

        private float GetPhaseProgress()
        {
            float elapsed = (float)(Time.GetTicksUsec() / 1_000_000.0 - Timestamp) * Speed * (float)Engine.TimeScale;
            float duration = GetCurrentPhaseDuration();
            return Mathf.Clamp(elapsed / duration, 0f, 1f);
        }

        private float GetCurrentPhaseDuration()
        {
            switch (phase)
            {
                case ActionPhase.Prepare: return prepareDuration;
                case ActionPhase.AttackImpact: return attackImpactDuration;
                case ActionPhase.TargetReaction: return targetReactionDuration;
                case ActionPhase.MoveForBack: return moveForBackDuration;
                case ActionPhase.Recover: return recoverDuration;
                default: return 1f;
            }
        }

        private void ResetTimestamp()
        {
            Timestamp = Time.GetTicksUsec() / 1_000_000.0;
        }

        protected override bool DoInternalAction()
        {
            float progress = GetPhaseProgress();

            switch (phase)
            {
                case ActionPhase.Prepare:
                {
                    float prepareProgress = EaseOutQuad(Mathf.Min(progress, 1f));
                    SourceControl.Rotation = Mathf.Lerp(SourceRot, TargetRot, prepareProgress);

                    if (progress >= 1f)
                    {
                        phase = ActionPhase.AttackImpact;
                        ResetTimestamp();
                        defendRotation = (float)GD.RandRange(-15f, 15f) * Mathf.Pi / 180f;

                        targetOriginalScale = TargetControl.Scale;
                        targetOriginalColor = TargetControl.Modulate;
                    }
                    break;
                }

                case ActionPhase.AttackImpact:
                {
                    float impactProgress = EaseInOutQuad(Mathf.Min(progress * 2f, 1f));
                    SourceControl.Rotation = Mathf.Lerp(TargetRot, defendRotation, impactProgress);

                    blockScale = targetOriginalScale * (1f + impactProgress * 0.1f);
                    TargetControl.Scale = blockScale;

                    blockColor = targetOriginalColor;
                    blockColor.G = Mathf.Min(1f, blockColor.G + impactProgress * 0.3f);
                    blockColor.B = Mathf.Min(1f, blockColor.B + impactProgress * 0.2f);
                    TargetControl.Modulate = blockColor;

                    if (!effectApplied && progress >= 0.4f)
                    {
                        effectApplied = true;
                        var result = ActionExecutorHub.Instance.Execute(Action, Source.Card, Target.Card);
                        Source.UpdateInfo();
                        Target.UpdateInfo();

                        if (result.Result && result.Target.Defence > 0)
                            ShowDamageBehaviour.SpawnDefence(result.Target.Defence, TargetControl, TargetControl.GlobalPosition);
                    }

                    if (progress >= 1f)
                    {
                        phase = ActionPhase.TargetReaction;
                        ResetTimestamp();
                    }
                    break;
                }

                case ActionPhase.TargetReaction:
                {
                    float reactionProgress = EaseInOutQuad(Mathf.Min(progress * 1.5f, 1f));

                    blockKnockback = TargetPos + (TargetPos - SourcePos).Normalized() * 0.1f;
                    TargetControl.GlobalPosition = TargetPos.Lerp(blockKnockback, reactionProgress * 0.5f);

                    TargetControl.Scale = blockScale.Lerp(targetOriginalScale, reactionProgress);

                    if (progress >= 1f)
                    {
                        phase = ActionPhase.MoveForBack;
                        ResetTimestamp();
                    }
                    break;
                }

                case ActionPhase.MoveForBack:
                {
                    float backProgress = EaseOutQuad(Mathf.Min(progress, 1f));
                    SourceControl.GlobalPosition = TargetPos.Lerp(SourcePos, backProgress);
                    SourceControl.Rotation = Mathf.Lerp(defendRotation, SourceRot, backProgress);

                    TargetControl.GlobalPosition = TargetControl.GlobalPosition.Lerp(TargetPos, backProgress);
                    TargetControl.Scale = TargetControl.Scale.Lerp(targetOriginalScale, backProgress);

                    Color currentColor = TargetControl.Modulate;
                    TargetControl.Modulate = currentColor.Lerp(targetOriginalColor, backProgress);

                    if (progress >= 1f)
                    {
                        phase = ActionPhase.Recover;
                        ResetTimestamp();
                    }
                    break;
                }

                case ActionPhase.Recover:
                {
                    float recoverProgress = EaseOutQuad(Mathf.Min(progress, 1f));

                    SourceControl.GlobalPosition = SourceControl.GlobalPosition.Lerp(SourcePos, recoverProgress);
                    SourceControl.Rotation = Mathf.Lerp(SourceControl.Rotation, SourceRot, recoverProgress);
                    TargetControl.Rotation = Mathf.Lerp(TargetControl.Rotation, TargetRot, recoverProgress);

                    if (progress >= 1f)
                        return true;
                    break;
                }

                default:
                    return true;
            }

            return false;
        }

        protected override void DoInternalEnd()
        {
            if (SourceControl != null)
            {
                SourceControl.GlobalPosition = SourcePos;
                SourceControl.Rotation = SourceRot;
            }
            if (TargetControl != null)
            {
                TargetControl.GlobalPosition = TargetPos;
                TargetControl.Rotation = TargetRot;
                TargetControl.Scale = targetOriginalScale;
                TargetControl.Modulate = targetOriginalColor;
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
    }
}
