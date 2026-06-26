namespace Cthangover.Core.Battle.Actions
{
    /// <summary>
    /// Typed executor source. Each IBattleCore returns its own provider,
    /// giving cores the ability to supply custom executor sets without
    /// polluting the global registry.
    /// </summary>
    public interface IActionExecutorProvider
    {
        IActionExecutor GetExecutor(string actionId);
    }
}
