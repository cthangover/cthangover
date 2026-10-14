namespace Cthangover.Mods.Interactive
{
    /// <summary>
    /// Subsystem migration marker: declares that the interactive subsystem now
    /// lives in this engine mod. Consumed by <c>SubsystemsBridge</c> for the
    /// coverage registry (see SubsystemsCoverageTestSetup.DeclaredTests).
    /// </summary>
    public static class SubsystemRegistration
    {
        /// <summary>Stable subsystem identifier used by the coverage registry.</summary>
        public static string SubsystemId => "interactive";
    }
}
