namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// Presentation mode of the studio dock. Both modes show the same session;
    /// only the "Now" tab renders it differently: <see cref="Author"/> as script
    /// text with a gutter, <see cref="Novice"/> as one card per line with plain
    /// wording, so a player who never saw the DSL can still follow and extend it.
    /// </summary>
    public enum StudioMode
    {
        Author,
        Novice,
    }
}
