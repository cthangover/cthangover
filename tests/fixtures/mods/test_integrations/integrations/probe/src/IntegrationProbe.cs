namespace Cthangover.Mods.TestIntegrations
{
    using Cthangover.Mods.Quests;

    /// <summary>
    /// The glue of the fixture's integration: it names a type of its partner <c>quests_base</c>, so it
    /// compiles only when the integration depends on the partner — the test sees the assembly loaded.
    /// </summary>
    public static class IntegrationProbe
    {
        /// <summary>A type of the partner mod, proving the integration compiles against it.</summary>
        public static readonly System.Type Partner = typeof(QuestsModInitializer);
    }
}
