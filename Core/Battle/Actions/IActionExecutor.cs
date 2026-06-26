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
        string ActionId { get; }

        ChangedAttributes Execute(ActionCharacter action, Character user, Character target);
    }
}
