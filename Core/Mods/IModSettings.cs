using System.Collections.Generic;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Interface for mods that expose user-configurable settings in the
    /// in-game settings menu. Implementations are auto-discovered via
    /// reflection in <see cref="ModSettingsRegistry"/> when a mod
    /// assembly is loaded.
    ///
    /// The contract mirrors <see cref="IModSaveable"/>: the mod only
    /// needs to declare its settings shape (<see cref="GetDefinitions"/>)
    /// and provide two symmetric methods for reading and writing values
    /// through a <see cref="DataBlob"/>. The core owns all persistence
    /// and UI rendering.
    ///
    /// Lifecycle:
    /// <list type="number">
    /// <item>On boot, the core loads persisted data from disk and calls
    ///   <see cref="ReadValues"/> so the mod applies its saved state.</item>
    /// <item>When the player opens the Settings menu, the core calls
    ///   <see cref="GetDefinitions"/> to build the UI, then
    ///   <see cref="WriteValues"/> to fill the controls with current values.</item>
    /// <item>When the player changes a setting, the core writes the new
    ///   value into a <c>DataBlob</c>, persists it, and calls
    ///   <see cref="ReadValues"/> so the mod reacts immediately.</item>
    /// </list>
    /// </summary>
    public interface IModSettings
    {
        /// <summary>
        /// Returns descriptors for every setting this mod exposes.
        /// Called every time the settings UI builds the controls
        /// for this mod. The list order defines the UI row order.
        /// </summary>
        IReadOnlyList<ModSettingDefinition> GetDefinitions();

        /// <summary>
        /// The mod serialises its current setting values into the
        /// supplied <c>blob</c>. The core uses this to populate the UI
        /// controls and to persist to disk. Use <c>blob.SetInt</c>,
        /// <c>blob.SetBool</c>, <c>blob.SetString</c>, etc.
        /// </summary>
        void WriteValues(DataBlob blob);

        /// <summary>
        /// The mod reads setting values from the supplied <c>blob</c>
        /// and applies them. Called on boot (from persisted data) and
        /// on every UI change (for immediate apply). Use
        /// <c>blob.GetInt</c>, <c>blob.GetBool</c>, etc. with fallback
        /// defaults.
        /// </summary>
        void ReadValues(DataBlob blob);
    }
}
