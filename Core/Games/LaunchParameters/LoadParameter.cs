namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Selects the save slot to load when entering the requested game.</summary>
    public sealed class LoadParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public LoadParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public LoadParameter(string value) : base(value?.Trim()) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "load";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Selects the save slot to load when entering the requested game.";
    }
}
