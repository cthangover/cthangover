using Cthangover.Core.UI.Executable;
using Cthangover.Mods.Dialog.Executable;

namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Builds scenario-backed executable events for the core SceneManager.
    /// The returned <see cref="ScenarioEvent"/> is parented to the requesting
    /// event chain by <c>IExecutableEventChain.AddEvent</c>, so the factory
    /// only needs to configure it.
    /// </summary>
    public class ScenarioEventFactory : IScenarioEventFactory
    {
        /// <summary>Builds a one-shot event that executes the inline scenario text (test-play path).</summary>
        public IExecutableEvent CreateFromText(string scenarioText)
        {
            return new ScenarioEvent { ScenarioText = scenarioText };
        }

        /// <summary>Builds an event from a scene event definition's scenario fields, remembering the source file.</summary>
        public IExecutableEvent CreateFromDefinition(string scenarioText, string scenarioId, string condition, bool? lightUseTime, bool isOneRun, string modId, string filePath)
        {
            return new ScenarioEvent
            {
                ScenarioText = scenarioText,
                ScenarioId = scenarioId,
                Condition = condition,
                LightUseTime = lightUseTime,
                IsOneRun = isOneRun,
                SourceModId = modId,
                SourcePath = filePath,
            };
        }
    }
}