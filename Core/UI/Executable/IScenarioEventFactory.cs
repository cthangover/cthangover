namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Factory for scenario-backed executable events. Implemented by the
    /// dialog_base engine mod and published through
    /// <c>AppServices.Current.SceneEvents.ScenarioEventFactory</c> so core scene
    /// code can build scenario events without referencing mod types.
    /// </summary>
    public interface IScenarioEventFactory
    {
        /// <summary>Builds a one-shot event that executes the inline <paramref name="scenarioText"/> (test-play path).</summary>
        IExecutableEvent CreateFromText(string scenarioText);

        /// <summary>
        /// Builds an event from a scene event definition's scenario fields.
        /// <paramref name="modId"/> and <paramref name="filePath"/> name the file
        /// the text came from, so tools (the scenario studio) can map a running
        /// event back to its source; both may be null for generated text.
        /// </summary>
        IExecutableEvent CreateFromDefinition(string scenarioText, string scenarioId, string condition, bool? lightUseTime, bool isOneRun, string modId, string filePath);
    }
}