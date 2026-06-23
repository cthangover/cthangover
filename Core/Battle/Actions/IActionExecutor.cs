using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle.Actions
{
    public interface IActionExecutor
    {
        string ActionId { get; }

        ChangedAttributes Execute(ActionCharacter action, Character user, Character target);
    }
}
