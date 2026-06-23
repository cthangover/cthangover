using Cthangover.Core.Characters;
using Cthangover.Core.Utils;

namespace Cthangover.FFBattle.Actions
{
    public class FFDefenceExecutor : ActionBase
    {
        public override string ID => "FFDefenceExecutor";

        public override ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(action, user))
                return new ChangedAttributes { Result = false };

            var defenceDelta = action.GetInt(ActionCharacter.ATTRIBUTE_DEFENCE, 4);
            target.Attributes.Defence.Value += defenceDelta;

            GameLogger.Log("FF_BATTLE",
                $"{user.Name} defends → +{defenceDelta} defence for {target?.Name}",
                LogLevel.Debug);

            return new ChangedAttributes { Result = true, Target = { Defence = defenceDelta } };
        }
    }
}
