using System;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Interface for mods that need to persist custom data alongside
    /// the core game save. Implementations are auto-discovered via
    /// reflection in <see cref="ModSaveableRegistry"/> when a mod
    /// assembly is loaded.
    ///
    /// <b>Contract:</b>
    /// <list type="number">
    /// <item><b>OnSave</b> — the mod receives a pre-filled slot
    ///   (ModId, ModName, ModVersion set by the core) and writes its
    ///   data, MinCoreVersion, IsRequired, and CustomData.</item>
    /// <item><b>OnLoad</b> — two variants:
    ///   <list type="bullet">
    ///     <item><b>Preferred:</b> override
    ///       <c>bool OnLoad(ModSlot, out string error)</c> — explicit
    ///       contract, no exceptions.</item>
    ///     <item><b>Legacy:</b> override
    ///       <c>void OnLoadVoid(ModSlot)</c> — throw on error, core
    ///       catches and reports.</item>
    ///   </list>
    /// </item>
    /// </list>
    /// </summary>
    public interface IModSaveable
    {
        /// <summary>
        /// Called by the core when saving the game. The mod receives a
        /// slot with <c>Metadata.ModId</c>, <c>Metadata.ModName</c>,
        /// and <c>Metadata.ModVersion</c> already filled by the core.
        /// The mod should set <c>MinCoreVersion</c>, <c>IsRequired</c>,
        /// <c>CustomData</c>, and write its state into
        /// <c>slot.Data</c>.
        /// </summary>
        void OnSave(Cthangover.Core.Settings.ModSlot slot);

        /// <summary>
        /// Preferred entry point for loading. Called by the core after
        /// phase-1 validation. Return <c>false</c> with a descriptive
        /// <c>errorMessage</c> if the data is incompatible or a
        /// migration is impossible. The core logs and shows the message
        /// to the user.
        ///
        /// Default implementation delegates to
        /// <see cref="OnLoadVoid"/>.
        /// </summary>
        bool OnLoad(Cthangover.Core.Settings.ModSlot slot, out string errorMessage)
        {
            OnLoadVoid(slot);
            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Legacy entry point for loading. Throw an exception with a
        /// descriptive message if the data is incompatible. Override
        /// <see cref="OnLoad(Cthangover.Core.Settings.ModSlot, out string)"/>
        /// instead for explicit error handling.
        ///
        /// The default throws <see cref="NotSupportedException"/> so
        /// that a mod must override at least one of the two methods.
        /// </summary>
        void OnLoadVoid(Cthangover.Core.Settings.ModSlot slot)
        {
            throw new NotSupportedException(
                "Override either OnLoad(ModSlot, out string errorMessage) " +
                "for explicit errors, or OnLoadVoid(ModSlot) for throw-based errors.");
        }
    }
}
