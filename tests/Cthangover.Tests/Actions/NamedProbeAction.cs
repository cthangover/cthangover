using Cthangover.Core.Actions;

namespace Engine.Tests.Actions
{
    /// <summary>
    /// Scenario action with a chosen name, for precedence tests. No parameterless
    /// constructor, so the factory's assembly scan never picks it up.
    /// </summary>
    public class NamedProbeAction : IScenarioAction
    {
        public NamedProbeAction(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public void Run(IActionContext context)
        {
        }
    }
}
