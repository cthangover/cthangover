using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle.Actions
{
    /// <summary>
    /// Executes a specific battle action. The executor is keyed by ActionId
    /// (matching the action's ID) so the hub can look it up without
    /// knowing the concrete type. Returns ChangedAttributes so callers
    /// can apply stat deltas and check whether the action succeeded.
    /// </summary>
    public interface IActionExecutor
    {
        /// <summary>
        /// String key that matches <see cref="ActionCharacter.ID"/>.
        /// The hub uses this to resolve executors without knowing
        /// concrete types.
        /// </summary>
        string ActionId { get; }

        /// <summary>
        /// Applies the action's effects. Receives the action definition
        /// and the user/target <see cref="Character"/> instances; returns
        /// <see cref="ChangedAttributes"/> with deltas and success/failure.
        /// </summary>
        ChangedAttributes Execute(ActionCharacter action, Character user, Character target);
    }
}
