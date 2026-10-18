namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// What a scenario line does, as <see cref="StudioLineReader"/> classifies it.
    /// The novice view only shows <see cref="Line"/>, <see cref="Choice"/> and
    /// <see cref="Option"/>; the rest is service lines it follows or hides.
    /// </summary>
    public enum StudioLineKind
    {
        Header,
        Comment,
        Light,
        Background,
        Line,
        Choice,
        Option,
        Label,
        Goto,
        Jump,
        Action,
        End,
        Command,
    }
}
