namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Contract for atomic scenario actions — named commands that the scenario DSL
    /// dispatches at runtime via ScenarioActionFactory. Each action has a unique
    /// Name (e.g. "quest.set_status", "battle.init") used as the registry key.
    /// Actions receive an IActionContext giving access to dialog variables and
    /// subsystem services (quests, battle, characters, lighting, scene, inventory).
    /// Implementations are discovered via reflection and require no manual
    /// registration — the factory scans all assemblies for IScenarioAction types.
    /// </summary>
    public interface IScenarioAction
    {
        string Name { get; }
        void Run(IActionContext context);
    }
}
