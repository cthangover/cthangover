namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// Marks the skills engine mod for subsystem coverage discovery: the
    /// coverage test scans loaded assemblies for this marker and requires a
    /// declared integration test for the subsystem.
    /// </summary>
    public static class SubsystemRegistration
    {
        public static string SubsystemId => "skills";
    }
}
