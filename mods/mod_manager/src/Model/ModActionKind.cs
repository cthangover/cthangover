namespace Mods.ModManager
{
    /// <summary>
    /// User-triggered mod operation. The screen resolves a tile's primary action
    /// via <see cref="ModActions.GetPrimary"/> and executes the kind here; the
    /// detail view maps its buttons to the same kinds. Every kind is persisted to
    /// disk immediately but only takes effect after a restart.
    /// </summary>
    public enum ModActionKind
    {
        /// <summary>Download and install the selected catalog version.</summary>
        Install,

        /// <summary>Install a newer catalog version over the local copy.</summary>
        Update,

        /// <summary>Install an older catalog version over the local copy.</summary>
        Downgrade,

        /// <summary>Reinstall the same version to replace locally modified files.</summary>
        Reinstall,

        /// <summary>Delete the installed mod (user mods root only), then clean per-mod state.</summary>
        Delete,

        /// <summary>Allow the mod to load on next start.</summary>
        Enable,

        /// <summary>Prevent the mod from loading on next start.</summary>
        Disable,
    }
}
