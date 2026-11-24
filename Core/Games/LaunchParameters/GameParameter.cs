namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Selects a game collection for the next boot or mod-set selection.</summary>
    public sealed class GameParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public GameParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public GameParameter(string value) : base(value?.Trim()) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "game";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Selects a game collection for the next boot or mod-set selection.";
    }
}
