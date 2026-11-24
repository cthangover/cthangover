namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Starts the legacy local DebugHost pipe at boot.</summary>
    public sealed class DevelopmentControlParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public DevelopmentControlParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public DevelopmentControlParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "development-control";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Starts the legacy local DebugHost pipe at boot.";
    }
}
