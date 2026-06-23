namespace Cthangover.Core.Battle.Actions
{
    public interface IActionExecutorProvider
    {
        IActionExecutor GetExecutor(string actionId);
    }
}
