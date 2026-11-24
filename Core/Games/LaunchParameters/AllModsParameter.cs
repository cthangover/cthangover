namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Loads the complete installed mod set before any game selection.</summary>
    public sealed class AllModsParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public AllModsParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public AllModsParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "all-mods";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Loads the complete installed mod set before any game selection.";
    }
}
