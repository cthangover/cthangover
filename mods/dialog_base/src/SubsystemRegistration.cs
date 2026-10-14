namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Migration marker for the coverage pipeline
    /// (<c>SubsystemsBridge</c> reflection scan): declares the dialog
    /// subsystem fully moved out of the core assembly into this engine mod.
    /// The id must match the <c>DeclaredTests</c> entry in
    /// <c>SubsystemsCoverageTestSetup</c>.
    /// </summary>
    public static class SubsystemRegistration
    {
        public static string SubsystemId => "dialog";
    }
}