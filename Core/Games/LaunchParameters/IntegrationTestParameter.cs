namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Names the requested integration suites. Several occurrences can activate different suites.</summary>
    public sealed class IntegrationTestParameter : StringListLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public IntegrationTestParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public IntegrationTestParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "itest";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Activates named integration suites. Separate suite IDs with commas or repeat the argument.";

        internal override bool RequestsDevelopmentMode => true;
        internal override bool ExcludesAutomaticHotReload => true;
    }
}
