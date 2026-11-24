namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Suppresses the mod file watcher and takes priority over hot-reload.</summary>
    public sealed class NoHotReloadParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public NoHotReloadParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public NoHotReloadParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "no-hot-reload";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Suppresses the mod file watcher and takes priority over hot-reload.";
    }
}
