namespace Cthangover.Mods.Quests
{
    /// <summary>
    /// Migration marker scanned by the integration coverage test
    /// (<c>SubsystemsBridge</c>): declares the quests subsystem as
    /// migrated out of the core assembly into this engine mod. The
    /// subsystem id must match the entry in
    /// <c>SubsystemsCoverageTestSetup.DeclaredTests</c> and
    /// <c>tests/tests.manifest.json</c>.
    /// </summary>
    public static class SubsystemRegistration
    {
        /// <summary>Subsystem id reported to the coverage test.</summary>
        public static string SubsystemId => "quests";
    }
}
