namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// One line of the scenario shown in the "Now" tab. <see cref="Text"/> is the
    /// DSL source for the author mode; the novice mode reads the structure
    /// instead: <see cref="Say"/> is what the player sees (a line, a question,
    /// an answer) and <see cref="Target"/> where the line points — the label of
    /// an option or goto, the name of a label, the scene of a switch, the id of
    /// an action. <see cref="StudioMoment"/> follows those links. The flags
    /// mirror what the runtime debugger will report: lines already executed, the
    /// line the dialog stopped on, breakpoints, lines added but not yet applied
    /// and a parse error message.
    /// </summary>
    public sealed class StudioCodeLine
    {
        public int Number { get; set; }
        public string Text { get; set; }
        public StudioLineKind Kind { get; set; }
        public StudioText Say { get; set; }
        public string Target { get; set; }
        public bool IsDone { get; set; }
        public bool IsCurrent { get; set; }
        public bool HasBreakpoint { get; set; }
        public bool IsNew { get; set; }
        public StudioText Error { get; set; }
    }
}
