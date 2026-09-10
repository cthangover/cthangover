namespace Cthangover.Core.Battle.Actions
{
    /// <summary>
    /// Typed executor source. Each IBattleCore returns its own provider,
    /// giving cores the ability to supply custom executor sets without
    /// polluting the global registry.
    /// </summary>
    public interface IActionExecutorProvider
    {
        /// <summary>
        /// Returns the executor registered for <paramref name="actionId"/>,
        /// or null if no override is configured. Called by
        /// <see cref="ActionExecutorHub"/> before falling back to the
        /// global registry.
        /// </summary>
        IActionExecutor GetExecutor(string actionId);
    }
}
