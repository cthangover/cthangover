namespace Engine.Tests.Scenarios
{
    /// <summary>
    /// Marks the ConditionStateMonitor tests as non-parallel. The monitor's
    /// <c>Version</c> is a global monotonic counter shared across the whole
    /// process, so its delta-based assertions must not run concurrently with
    /// other test classes that also publish events (e.g. ScenarioConditionTests).
    /// </summary>
    [CollectionDefinition("ConditionStateMonitor", DisableParallelization = true)]
    public class ConditionStateMonitorCollection
    {
    }
}
