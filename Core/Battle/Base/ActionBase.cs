
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
        
        /// <summary>
        /// Maps to the abstract <see cref="ID"/> so the hub can resolve
        /// this executor by action name.
        /// </summary>
        public string ActionId => ID;

        /// <summary>
        /// Unique action identifier that matches
        /// <see cref="ActionCharacter.ID"/> in data.
        /// </summary>
        public abstract string            ID { get; }
        /// <summary>
        /// Applies the action's effects to <paramref name="target"/>
        /// using <paramref name="user"/>'s stats and the
        /// <paramref name="action"/> definition. Returns stat deltas
        /// and a success/failure flag.
        /// </summary>
        public abstract ChangedAttributes Execute(ActionCharacter action, Character user, Character target);

        /// <summary>
        /// Validates that <paramref name="user"/> has at least
        /// <c>ATTRIBUTE_REQUIRED_POINT</c> remaining, deducts it, and
        /// returns <c>true</c>. Returns <c>false</c> with a warning log
        /// if the user lacks sufficient points, allowing the action to
        /// fail gracefully.
        /// </summary>
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
