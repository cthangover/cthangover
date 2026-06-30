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
        /// <summary>
        /// Unique action identifier used as the registry key in
        /// ScenarioActionFactory. Follows the "subsystem.verb" convention
        /// (e.g. "quest.set_status", "battle.init", "scene.instantiate").
        /// This is the name that scenario DSL scripts use after the "action"
        /// keyword. Must be unique across all registered actions — if a
        /// mod registers an action with a duplicate name, it is silently
        /// ignored (earliest registration wins).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Executes the action using the provided context. The context
        /// provides access to dialog variables (via GetParam) and all
        /// subsystem services. Run() is called synchronously by the dialog
        /// engine when it encounters the corresponding action command in
        /// a scenario script — the dialog pauses until Run() returns, so
        /// actions must not block indefinitely.
        /// </summary>
        void Run(IActionContext context);
    }
}
