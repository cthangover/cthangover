namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Starts the requested collection with fresh game data.</summary>
    public sealed class NewGameParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public NewGameParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public NewGameParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "new";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Starts the requested collection with fresh game data.";
    }
}
