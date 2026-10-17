using System.Collections.Generic;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Declares a single setting that a mod exposes in the settings UI.
    /// <c>Key</c> is the unique identifier used to read/write values in
    /// <see cref="DataBlob"/>; <c>Name</c> is a localisation key (resolved
    /// via <c>TranslationServer.Translate</c>) shown as the label.
    ///
    /// <c>Type</c> determines which Godot control is spawned in the UI:
    /// <list type="bullet">
    /// <item><b>Bool</b> → <c>CheckButton</c></item>
    /// <item><b>Integer</b> → <c>SpinBox</c> (no decimals)</item>
    /// <item><b>Float</b> → <c>SpinBox</c> (with decimals)</item>
    /// <item><b>String</b> → <c>LineEdit</c></item>
    /// <item><b>Enum</b> → <c>OptionButton</c> (populated from <c>Options</c>)</item>
    /// <item><b>Slider</b> → <c>HSlider</c> + value label (bound by <c>Min</c>/<c>Max</c>/<c>Step</c>)</item>
    /// </list>
    ///
    /// <c>DefaultValue</c> is a string representation of the initial value,
    /// used as a fallback when no persisted value exists and for the Reset
    /// button.
    /// </summary>
    public class ModSettingDefinition
    {
        /// <summary>Unique setting key within this mod (e.g. "atb_speed").</summary>
        public string Key { get; set; }

        /// <summary>
        /// Localisation key shown as the label next to the control.
        /// The mod must provide the translation via its <c>locale/</c> files.
        /// </summary>
        public string Name { get; set; }

        /// <summary>Widget type spawned in the UI for this setting.</summary>
        public SettingType Type { get; set; }

        /// <summary>
        /// String representation of the default value. Used when no
        /// persisted value exists and when the user presses Reset.
        /// Interpreted by the UI according to <c>Type</c>.
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Applicable only when <c>Type == SettingType.Enum</c>.
        /// Maps internal values (e.g. "fast") to localisation keys
        /// for display (e.g. "mod_ff/atb_fast").
        /// </summary>
        public Dictionary<string, string> Options { get; set; }

        /// <summary>Applicable only when <c>Type == SettingType.Slider</c>.</summary>
        public float Min { get; set; }

        /// <summary>Applicable only when <c>Type == SettingType.Slider</c>.</summary>
        public float Max { get; set; }

        /// <summary>Applicable only when <c>Type == SettingType.Slider</c>.</summary>
        public float Step { get; set; }

        /// <summary>
        /// <c>SettingType.Action</c> only: button caption (locale key). The
        /// action runs through <see cref="IModSettingsActions.RunAction"/>.
        /// </summary>
        public string ActionText { get; set; }

        /// <summary><c>SettingType.Action</c> only: confirmation question (locale key); null runs immediately.</summary>
        public string ConfirmText { get; set; }
    }

    /// <summary>
    /// Widget type for a mod setting. Determines which Godot control
    /// is spawned and how the value is read/written.
    /// </summary>
    public enum SettingType
    {
        Bool,
        Integer,
        Float,
        String,
        Enum,
        Slider,

        /// <summary>A button that runs a mod command (no stored value), e.g. purging data.</summary>
        Action
    }
}
