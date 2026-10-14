namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Contract for events carrying a scenario condition expression. Implemented
    /// by the dialog mod's ScenarioEvent; SceneManager assigns conditions through
    /// this interface without referencing the concrete mod type.
    /// </summary>
    public interface IConditionedEvent
    {
        /// <summary>Condition expression evaluated by the event before triggering. Empty means always pass.</summary>
        string Condition { get; set; }
    }
}