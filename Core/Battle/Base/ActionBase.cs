
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Characters
{

    /// <summary>
    /// Convenience base for IActionExecutor implementations. Provides
    /// CheckRequiredAndUsePoint which validates that the user has enough
    /// "Point" attribute (a generic action-point / mana pool) before
    /// deducting it — returning false lets the action fail gracefully.
    /// ID is abstract so each action declares its own identity.
    /// </summary>
    public abstract class ActionBase : IActionExecutor
    {
        
        public string ActionId => ID;

        public abstract string            ID { get; }
        public abstract ChangedAttributes Execute(ActionCharacter action, Character user, Character target);

        protected bool CheckRequiredAndUsePoint(ActionCharacter action, Character user)
        {
            var requiredPoint = action.GetInt(ActionCharacter.ATTRIBUTE_REQUIRED_POINT);

            if (user.Attributes.Point.Value < requiredPoint)
            {
                GameLogger.Log("BATTLE", user.ID + " not checked point required [" + user.Attributes.Point.Value + "] for action " + action.ID + " [" + requiredPoint + "]", LogLevel.Warning);
                return false;
            }
            
            GameLogger.Log("BATTLE", user.ID + " use [" + requiredPoint + "] points for action " + action.ID, LogLevel.Debug);

            user.Attributes.Point.Value -= requiredPoint;
            return true;
        }
        
    }

}
