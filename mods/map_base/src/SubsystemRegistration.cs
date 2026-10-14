namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Marks the world map engine mod for subsystem coverage discovery: the
    /// coverage test scans loaded assemblies for this marker and requires a
    /// declared integration test for the subsystem.
    /// </summary>
    public static class SubsystemRegistration
    {
        public static string SubsystemId => "map";
    }
}
