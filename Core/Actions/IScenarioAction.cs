namespace Cthangover.Core.Actions
{
    public interface IScenarioAction
    {
        string Name { get; }
        void Run(IActionContext context);
    }
}
