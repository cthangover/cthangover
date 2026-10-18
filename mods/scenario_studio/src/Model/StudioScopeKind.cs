namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// How the studio may touch content of a mod, shown as the colour and caption
    /// of the mod scope badge. Players mostly run zip mods, which are read-only:
    /// edits of their scenarios land in the studio layer that overrides them,
    /// and the badge pair "layer overrides zip" tells whose content is affected.
    /// </summary>
    public enum StudioScopeKind
    {
        /// <summary>Folder mod: edits are written into the mod itself.</summary>
        Own,
        /// <summary>Zip mod: read-only, edits go to the studio layer over it.</summary>
        Overlay,
        /// <summary>The studio layer of the current game that holds the player's edits.</summary>
        Layer,
        /// <summary>Compiled code of a mod: shown, never edited from the studio.</summary>
        ReadOnly,
    }
}
