namespace Engine.Tests.Boot
{
    /// <summary>
    /// AppServices.Current is process-wide static state shared by every test
    /// class that touches the composition root. All such tests join this
    /// collection so they never run in parallel with each other or with other
    /// collections (same scheme as the existing "ConditionStateMonitor"
    /// collection used by MapZoneServiceTests and friends).
    /// </summary>
    [CollectionDefinition("appservices", DisableParallelization = true)]
    public class AppServicesCollectionDefinition
    {
    }
}
