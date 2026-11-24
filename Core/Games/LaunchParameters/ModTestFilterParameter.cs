namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Filters the mod test cases to execute by their IDs or patterns.</summary>
    public sealed class ModTestFilterParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public ModTestFilterParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public ModTestFilterParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "test-filter";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Filters the mod test cases to execute by their IDs or patterns.";
    }
}
