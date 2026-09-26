namespace Mods.ModManager
{
    /// <summary>
    /// UI-level state of a mod tile. Combines the local install state with the
    /// online catalog comparison so a single value drives the tile badge and the
    /// contextual action button. <see cref="ModifiedLocally"/> means the local
    /// content hash differs from the catalog entry of the same version — the
    /// player edited the mod and overwriting would lose their changes.
    /// </summary>
    public enum ModItemStatus
    {
        /// <summary>No local copy exists; only the catalog entry is shown.</summary>
        NotInstalled,

        /// <summary>Installed and up-to-date with the selected catalog version (or no catalog entry).</summary>
        Installed,

        /// <summary>The catalog has a newer version than the installed one.</summary>
        UpdateAvailable,

        /// <summary>The catalog's selected version is older than the installed one.</summary>
        DowngradeAvailable,

        /// <summary>Same version as the catalog, but the local content hash differs.</summary>
        ModifiedLocally,

        /// <summary>Installed but disabled by the player (<c>disabled_mods.json</c>).</summary>
        Disabled,
    }
}
