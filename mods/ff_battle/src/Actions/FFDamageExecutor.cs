using Cthangover.Core.Characters;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Executes a physical attack action. Calculates damage as
    /// <c>action.Attack × user.Attack</c>, then routes through
    /// <see cref="StatusEffectQueue.OnDealDamage"/> and
    /// <see cref="StatusEffectQueue.OnTakeDamage"/> for status-effect
    /// hooks. Subtraction is applied defence-first: <see cref="CharacterAttributes.Defence"/>
    /// absorbs damage point-for-point before health is reduced.
    /// Registered under ID <c>"physics/attack"</c>.
    /// </summary>
    public class FFDamageExecutor : ActionBase
    {
        public override string ID => "FFDamageExecutor";

        public override ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(action, user))
                return new ChangedAttributes { Result = false };

            var damageDelta = Mathf.RoundToInt(action.GetFloat(ActionCharacter.ATTRIBUTE_ATTACK, 1f) * user.Attributes.Attack.Value);
            target.StatusEffectQueue.OnTakeDamage(user, ref damageDelta);
            user.StatusEffectQueue.OnDealDamage(target, ref damageDelta);

            var defenceDelta = 0;
            if (target.Attributes.Defence.Value > 0)
            {
                defenceDelta = target.Attributes.Defence.Value >= damageDelta
                    ? damageDelta
                    : target.Attributes.Defence.Value;
                damageDelta -= defenceDelta;
            }

            target.Attributes.Defence.Value -= defenceDelta;
            target.Attributes.Health.Value -= damageDelta;

            GameLogger.Log("FF_BATTLE",
                $"{user.Name} attacks {target.Name}: damage={damageDelta} defence={defenceDelta}",
                LogLevel.Debug);

            return new ChangedAttributes
            {
                Result = true,
                Target = { Damage = damageDelta, Defence = -defenceDelta }
            };
        }
    }
}
