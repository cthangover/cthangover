namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Optional companion of <see cref="IModSettings"/> for settings of type
    /// <see cref="SettingType.Action"/>: the settings screen shows them as
    /// buttons and calls <see cref="RunAction"/> on press (after the optional
    /// confirmation).
    /// </summary>
    public interface IModSettingsActions
    {
        /// <summary>Runs the action with the given setting key; returns a short result message for the player.</summary>
        string RunAction(string key);
    }
}
