namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Starts the mod file watcher even outside the Godot editor.</summary>
    public sealed class HotReloadParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public HotReloadParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public HotReloadParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "hot-reload";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Starts the mod file watcher even outside the Godot editor.";
    }
}
