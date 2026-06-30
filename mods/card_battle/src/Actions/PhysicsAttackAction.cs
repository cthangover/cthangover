using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Utils;
using Cthangover.Core.Characters;
using Cthangover.CardBattle.UI;
using Godot;

namespace Cthangover.CardBattle.Actions
{
    /// <summary>
    /// Animated battle action that moves the source card toward an enemy target, applies damage/defence
    /// via <see cref="ActionExecutorHub"/>, and animates the target's knockback/squash reaction.
    /// The source card follows one of five randomly-selected <see cref="MovementPattern"/> trajectories
    /// (direct, arc, zigzag, spiral, dash). This is the visual counterpart of <see cref="PhysicsDamageActionCard"/>
    /// — it handles the presentation while the executor handles the stat changes.
    /// Created by <see cref="CardBattleCore.CreateAnimatedAction"/> for enemy turns.
    /// </summary>
    public class PhysicsAttackAction : AbstractBattleAction
    {
        /// <summary>
        /// Pre-defined movement trajectory patterns for the source card's approach to the target.
        /// Selected randomly at construction to add visual variety to enemy attack animations.
        /// </summary>
        public enum MovementPattern
        {
            Direct,
            Arc,
            Zigzag,
            Spiral,
            Dash
        }

        private ActionPhase phase = ActionPhase.Wait;
        private float attackRotation;
        private bool damageApplied;
        private bool stunEffectSpawned;

        private Vector2 targetOriginalPos;
        private Vector2 targetOriginalScale;
        private Vector2 knockbackDirection;
        private float targetReactionIntensity = 0.3f;

        private Vector2 knockbackPos;
        private float shake;
        private Vector2 squashScale;

        private float waitDuration = 0.2f;
        private float prepareDuration = 0.3f;
        private float moveForAttackDuration = 0.4f;
        private float attackImpactDuration = 0.3f;
        private float targetReactionDuration = 0.4f;
        private float moveForBackDuration = 0.5f;
        private float recoverDuration = 0.3f;

        private MovementPattern movementPattern = MovementPattern.Direct;
        private Vector2 movementControlPoint;

        /// <summary>
        /// Creates an attack animation from <paramref name="source"/> to <paramref name="target"/>
        /// using the stats defined in <paramref name="action"/>. The <see cref="MovementPattern"/> is
        /// randomly selected on construction to make enemy attacks visually distinct.
        /// <paramref name="speed"/> acts as a multiplier on all phase durations.
        /// </summary>
        public PhysicsAttackAction(CharacterCardNode source, CharacterCardNode target, ActionCharacter action, float speed = 1)
            : base(source, target, action, speed)
        {
            movementPattern = (MovementPattern)GD.RandRange(0, 5);
        }

        protected override void DoInternalStart()
        {
            Timestamp = Time.GetTicksUsec() / 1_000_000.0;
            damageApplied = false;
            stunEffectSpawned = false;
            phase = ActionPhase.Wait;
            targetOriginalScale = TargetControl.Scale;
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
                case ActionPhase.Wait: return waitDuration;
                case ActionPhase.Prepare: return prepareDuration;
                case ActionPhase.MoveForAttack: return moveForAttackDuration;
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
                case ActionPhase.Wait:
                    if (progress >= 1f)
                    {
                        phase = ActionPhase.Prepare;
                        ResetTimestamp();
                        targetOriginalPos = TargetControl.GlobalPosition;
                        targetOriginalScale = TargetControl.Scale;
                        knockbackDirection = (TargetControl.GlobalPosition - SourceControl.GlobalPosition).Normalized();
                        InitializeMovementPattern();
                    }
                    break;

                case ActionPhase.Prepare:
                {
                    float prepareProgress = EaseOutQuad(Mathf.Min(progress, 1f));
                    Vector2 preparePosition = GetMovementPosition(prepareProgress * 0.1f);
                    SourceControl.GlobalPosition = SourcePos.Lerp(preparePosition, prepareProgress);
                    SourceControl.Rotation = Mathf.Lerp(SourceRot, TargetRot, prepareProgress * 0.3f);

                    if (progress >= 1f)
                    {
                        phase = ActionPhase.MoveForAttack;
                        ResetTimestamp();
                    }
                    break;
                }

                case ActionPhase.MoveForAttack:
                {
                    float attackProgress = EaseInOutQuad(Mathf.Min(progress, 1f));
                    Vector2 targetPosition = GetMovementPosition(attackProgress);
                    SourceControl.GlobalPosition = targetPosition;
                    SourceControl.Rotation = Mathf.Lerp(SourceRot, TargetRot, attackProgress);

                    if (progress >= 1f)
                    {
                        phase = ActionPhase.AttackImpact;
                        ResetTimestamp();
                        attackRotation = (float)GD.RandRange(-35f, 35f) * Mathf.Pi / 180f;
                    }
                    break;
                }

                case ActionPhase.AttackImpact:
                {
                    float impactProgress = EaseInOutQuad(Mathf.Min(progress * 3f, 1f));
                    SourceControl.Rotation = Mathf.Lerp(TargetRot, attackRotation, impactProgress);

                    if (!damageApplied && progress >= 0.3f)
                    {
                        damageApplied = true;
                        var result = ActionExecutorHub.Instance.Execute(Action, Source.Card, Target.Card);
                        Source.UpdateInfo();
                        Target.UpdateInfo();

                        if (result.Result)
                        {
                            if (result.Target.Damage > 0)
                                ShowDamageBehaviour.SpawnDamage(result.Target.Damage, TargetControl, TargetControl.GlobalPosition);

                            var defenceLost = -result.Target.Defence;
                            if (defenceLost > 0)
                                ShowDamageBehaviour.SpawnDefence(defenceLost, TargetControl, TargetControl.GlobalPosition);
                        }
                        else
                        {
                            GameLogger.Log("CARD_ACT",
                                $"PhysicsAttackAction: failed to execute action {Action.Name} (source={Source.Card.Name} target={Target.Card.Name})",
                                LogLevel.Error);
                        }
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
                    float reactionProgress = EaseInOutQuad(Mathf.Min(progress, 1f));

                    bool isStun = Action != null && Action.GetInt(ActionCharacter.ATTRIBUTE_TURN, 0) > 1;
                    float currentKnockback = targetReactionIntensity;
                    if (isStun)
                        currentKnockback *= 2.5f;

                    knockbackPos = targetOriginalPos + knockbackDirection * currentKnockback;
                    TargetControl.GlobalPosition = targetOriginalPos.Lerp(knockbackPos, reactionProgress);

                    squashScale = targetOriginalScale;
                    float currentSquash = isStun ? 0.35f : 0.2f;
                    squashScale.X *= 1f - reactionProgress * currentSquash;
                    squashScale.Y *= 1f + reactionProgress * (currentSquash * 0.5f);
                    TargetControl.Scale = squashScale;

                    float currentShakeMultiplier = isStun ? 1.8f : 1f;
                    shake = Mathf.Sin(reactionProgress * Mathf.Pi * (isStun ? 6f : 4f)) * (1f - reactionProgress) * 0.05f * currentShakeMultiplier;
                    TargetControl.Rotation = shake * 30f * Mathf.Pi / 180f;

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
                    Vector2 backPosition = GetMovementPosition(1f - progress);
                    SourceControl.GlobalPosition = backPosition.Lerp(SourcePos, backProgress);
                    SourceControl.Rotation = Mathf.Lerp(attackRotation, SourceRot, backProgress);

                    TargetControl.GlobalPosition = knockbackPos.Lerp(targetOriginalPos, backProgress);
                    TargetControl.Scale = squashScale.Lerp(targetOriginalScale, backProgress);
                    float currentShake = Mathf.Sin(backProgress * Mathf.Pi) * shake * (1f - backProgress);
                    TargetControl.Rotation = Mathf.Lerp(currentShake * 30f * Mathf.Pi / 180f, TargetRot, backProgress);

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
                    TargetControl.GlobalPosition = TargetControl.GlobalPosition.Lerp(TargetPos, recoverProgress);
                    TargetControl.Rotation = Mathf.Lerp(TargetControl.Rotation, TargetRot, recoverProgress);
                    TargetControl.Scale = TargetControl.Scale.Lerp(targetOriginalScale, recoverProgress);

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
            }
        }

        private void InitializeMovementPattern()
        {
            Vector2 direction = (TargetPos - SourcePos).Normalized();
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            switch (movementPattern)
            {
                case MovementPattern.Arc:
                    movementControlPoint = SourcePos + direction * 0.5f + perpendicular * 2f;
                    break;

                case MovementPattern.Zigzag:
                    movementControlPoint = SourcePos + direction * 0.5f + perpendicular * 1.5f;
                    break;

                case MovementPattern.Spiral:
                    movementControlPoint = TargetPos + perpendicular * 1.5f;
                    break;

                case MovementPattern.Dash:
                    movementControlPoint = SourcePos + direction * 0.3f;
                    break;

                default:
                    movementControlPoint = TargetPos;
                    break;
            }
        }

        private Vector2 GetMovementPosition(float progress)
        {
            switch (movementPattern)
            {
                case MovementPattern.Direct:
                    return SourcePos.Lerp(TargetPos, progress);

                case MovementPattern.Arc:
                    return SourcePos.Lerp(movementControlPoint, progress)
                        .Lerp(movementControlPoint.Lerp(TargetPos, progress), progress);

                case MovementPattern.Zigzag:
                    if (progress < 0.5f)
                        return SourcePos.Lerp(movementControlPoint, progress * 2f);
                    else
                        return movementControlPoint.Lerp(TargetPos, (progress - 0.5f) * 2f);

                case MovementPattern.Spiral:
                {
                    Vector2 toTarget = TargetPos - SourcePos;
                    float angle = progress * Mathf.Pi * 2f;
                    float radius = (1f - progress) * 1.5f;
                    Vector2 spiralOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    return SourcePos.Lerp(TargetPos, progress) + spiralOffset;
                }

                case MovementPattern.Dash:
                    if (progress < 0.3f)
                        return SourcePos.Lerp(movementControlPoint, progress / 0.3f);
                    else if (progress < 0.7f)
                        return movementControlPoint;
                    else
                        return movementControlPoint.Lerp(TargetPos, (progress - 0.7f) / 0.3f);

                default:
                    return SourcePos.Lerp(TargetPos, progress);
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
