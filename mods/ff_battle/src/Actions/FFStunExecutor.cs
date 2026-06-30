using Cthangover.Core.Characters;
using Cthangover.Core.Utils;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Applies a stun status effect to the target for a configurable number of turns.
    /// Reads the turn count from <see cref="ActionCharacter.ATTRIBUTE_TURN"/> (default 3).
    /// Stunned characters are skipped during turn iteration in
    /// <see cref="FFBattleCore.TryNextCharacterOrEndTurn"/> and
    /// <see cref="FFBattleCore.RunEnemyTurn"/>. Registered under ID <c>"physics/stun"</c>.
    /// </summary>
    public class FFStunExecutor : ActionBase
    {
        public override string ID => "FFStunExecutor";

        public override ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(action, user))
                return new ChangedAttributes { Result = false };

            var turns = action.GetInt(ActionCharacter.ATTRIBUTE_TURN, 3);
            target.StatusEffectQueue.Add("effect/physics/stun", turns);

            GameLogger.Log("FF_BATTLE",
                $"{user.Name} stuns {target.Name} for {turns} turns",
                LogLevel.Debug);

            return new ChangedAttributes { Result = true };
        }
    }
}
