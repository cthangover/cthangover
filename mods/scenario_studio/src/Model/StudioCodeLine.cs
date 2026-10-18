namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// One line of the scenario shown in the "Now" tab. Carries both renderings:
    /// <see cref="Text"/> is the DSL source for the author mode and
    /// <see cref="NoviceText"/> the plain wording for the novice cards. The flags
    /// mirror what the runtime debugger will report: lines already executed, the
    /// line the dialog stopped on, breakpoints, lines added but not yet applied
    /// and a parse error message.
    /// </summary>
    public sealed class StudioCodeLine
    {
        public int Number { get; set; }
        public string Text { get; set; }
        public StudioLineKind Kind { get; set; }
        public StudioText NoviceText { get; set; }
        public bool IsDone { get; set; }
        public bool IsCurrent { get; set; }
        public bool HasBreakpoint { get; set; }
        public bool IsNew { get; set; }
        public StudioText Error { get; set; }
    }
}
